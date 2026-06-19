using System;
using System.Management;

namespace ForzaInstallation.Evasion
{
    internal static class SandboxCheck
    {
        internal static bool IsSandbox()
        {
            try
            {
                if (Environment.ProcessorCount < 2) return true;
                using (var mos = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                using (var c = mos.Get())
                {
                    foreach (var o in c)
                    {
                        var mem = Convert.ToUInt64(o["TotalPhysicalMemory"]);
                        if (mem < 1024L * 1024 * 1024) return true;
                    }
                }
            }
            catch { }
            return false;
        }
    }
}
