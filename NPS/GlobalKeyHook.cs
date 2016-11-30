using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// ReSharper disable InconsistentNaming
namespace NPS
{
    public class GlobalKeyHook
    {
        public struct KeyBoardHookStruct
        {
            public int VkCode;
            public int ScanCode;
            public int Flags;
            public int Time;
            public int DwExtraInfo;
        }

        public delegate int LlKeyBoardHook(int code, int wParam, ref KeyBoardHookStruct lParam);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private readonly LlKeyBoardHook llkh;
        public List<Keys> HookedKeys = new List<Keys>();

        IntPtr Hook = IntPtr.Zero;

        public event KeyEventHandler KeyDown;
        public event KeyEventHandler KeyUp;

        [DllImport("user32.dll")]
        private static extern int CallNextHookEx(IntPtr hhk, int code, int wParam, ref KeyBoardHookStruct lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LlKeyBoardHook callback, IntPtr hInstance, uint threadid);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hInstance);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string lpFileName);

        public GlobalKeyHook()
        {
            llkh = HookProc;
        }

        ~GlobalKeyHook()
        {
            unhook();
        }

        public int HookProc(int code, int wParam, ref KeyBoardHookStruct lParam)
        {
            if (code >= 0)
            {
                var key = (Keys) lParam.VkCode;
                if (HookedKeys.Contains(key))
                {
                    var Arg = new KeyEventArgs(key);
                    switch (wParam)
                    {
                        case WM_KEYDOWN:
                        case WM_SYSKEYDOWN:
                            KeyDown?.Invoke(this, Arg);
                            break;
                        case WM_KEYUP:
                        case WM_SYSKEYUP:
                            KeyUp?.Invoke(this, Arg);
                            break;
                    }
                
                    if (Arg.Handled)
                        return 1;
                }
            }

            return CallNextHookEx(Hook, code, wParam, ref lParam);
        }

        public void hook()
        {
            var hInstance = LoadLibrary("User32");
            Hook = SetWindowsHookEx(WH_KEYBOARD_LL, llkh, hInstance, 0);
        }

        public void unhook()
        {
            UnhookWindowsHookEx(Hook);
        }
    }
}