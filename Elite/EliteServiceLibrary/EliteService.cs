/// <author>
/// Shawn Quereshi
/// </author>
namespace EliteServiceLibrary
{
    using System;
    using System.Runtime.InteropServices;

    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "Service1" in both code and config file together.
    public class EliteService : IEliteService
    {
        [DllImport("user32.dll")]
        internal static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);
        
        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint uCode, uint uMapType);
        
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            internal InputType type;
            internal InputUnion U;
            internal static int Size
            {
                get { return Marshal.SizeOf<INPUT>(); }
            }
        }
        internal enum InputType : uint
        {
            KEYBOARD = 1
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct InputUnion
        {
            [FieldOffset(0)]
            internal MOUSEINPUT mi;
            [FieldOffset(0)]
            internal KEYBDINPUT ki;
            [FieldOffset(0)]
            internal HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MOUSEINPUT
        {
            internal Int32 dx;
            internal Int32 dy;
            internal UInt32 mouseData;
            internal UInt32 dwFlags;
            internal UInt32 time;
            internal IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HARDWAREINPUT
        {
            internal UInt32 uMsg;
            internal UInt16 wParamL;
            internal UInt16 wParamH;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct KEYBDINPUT
        {
            internal UInt16 virtualKey;
            internal UInt16 scanCode;
            internal KEYEVENTF dwFlags;
            internal UInt32 time;
            internal IntPtr dwExtraInfo;
        }

        [Flags]
        internal enum KEYEVENTF : UInt32
        {
            EXTENDEDKEY = 0x0001,
            KEYUP = 0x0002,
            SCANCODE = 0x0008,
            UNICODE = 0x0004
        }

        /// <summary>
        /// Sends a key down signal to the OS.
        /// </summary>
        /// <param name="virtualKey">The virtual key.</param>
        public void SendKeyDown(ushort virtualKey)
        {
            var scan = (ushort)MapVirtualKey(virtualKey, 0);
            var keyboardFlags = KEYEVENTF.SCANCODE;
            var keyboardInput = new KEYBDINPUT() { virtualKey = 0, scanCode = scan, dwFlags = keyboardFlags, time = 0, dwExtraInfo = IntPtr.Zero };
            var wrapper = new InputUnion() { ki = keyboardInput };
            var down = new INPUT() { type = InputType.KEYBOARD, U = wrapper };
            SendInput(1, new INPUT[] { down }, INPUT.Size);
        }

        /// <summary>
        /// Sends a key up signal to the OS.
        /// </summary>
        /// <param name="virtualKey">The virtual key.</param>
        public void SendKeyUp(ushort virtualLKey)
        {
            var scan = (ushort)MapVirtualKey(virtualLKey, 0);
            var keyboardFlags = KEYEVENTF.KEYUP | KEYEVENTF.SCANCODE;
            var keyboardInput = new KEYBDINPUT() { virtualKey = 0, scanCode = scan, dwFlags = keyboardFlags, time = 0, dwExtraInfo = IntPtr.Zero };
            var wrapper = new InputUnion() { ki = keyboardInput };
            var up = new INPUT() { type = InputType.KEYBOARD, U = wrapper };
            SendInput(1, new INPUT[] { up }, INPUT.Size);
        }
    }
}
