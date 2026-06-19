using Microsoft.Win32;

namespace ForzaInstallation.Persistence
{
    internal static class RegistryRun
    {
        internal static string InstallHkcu()
        {
            var result = "";
            var path = System.Reflection.Assembly.GetExecutingAssembly().Location;
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null) { key.SetValue("ForzaInstallationUpdater", path); result = "HKCU:OK;"; }
                }
            }
            catch { result = "HKCU:FAIL;"; }
            return "Registry HKCU: " + result;
        }

        internal static string InstallHklm()
        {
            var result = "";
            var path = System.Reflection.Assembly.GetExecutingAssembly().Location;
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null) { key.SetValue("ForzaInstallationUpdater", path); result = "HKLM:OK;"; }
                }
            }
            catch { result = "HKLM:FAIL;"; }
            return "Registry HKLM: " + result;
        }

        internal static string Install()
        {
            return InstallHkcu() + " " + InstallHklm();
        }

        internal static string RemoveAll()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null) key.DeleteValue("ForzaInstallationUpdater", false);
                }
            }
            catch { }
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null) key.DeleteValue("ForzaInstallationUpdater", false);
                }
            }
            catch { }
            return "Registry removed";
        }
    }
}
