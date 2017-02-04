using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Input.Preview.Injection;

namespace EliteUi
{
    public class InjectionEngine
    {
        [DllImport(@"C:\Windows\System32\user32.dll", ExactSpelling = true)]
        static extern uint MapVirtualKeyW(uint uCode, uint uMapType);
        
        private static InjectionEngine instance;

        public static InjectionEngine Instance
        {
            get
            {
                return instance ?? (instance = new InjectionEngine());
            }
        }

        private Dictionary<VirtualKey, IEnumerable<InjectedInputKeyboardInfo>> keyDownCache = null;
        private Dictionary<VirtualKey, IEnumerable<InjectedInputKeyboardInfo>> keyUpCache = null;
        private InputInjector injector = null;

        private InjectionEngine()
        {
            injector = InputInjector.TryCreate();
            keyDownCache = new Dictionary<VirtualKey, IEnumerable<InjectedInputKeyboardInfo>>();
            keyUpCache = new Dictionary<VirtualKey, IEnumerable<InjectedInputKeyboardInfo>>();
        }

        public void SendKeyDown(VirtualKey virtualKey)
        {
            IEnumerable<InjectedInputKeyboardInfo> keyDown;
            if (!keyDownCache.TryGetValue(virtualKey, out keyDown))
            { 
                var kd = new InjectedInputKeyboardInfo();
                kd.KeyOptions = InjectedInputKeyOptions.ScanCode;// InjectedInputKeyOptions.ScanCode;
                kd.ScanCode = (ushort)MapVirtualKeyW((ushort)virtualKey, 0);
                //kd.VirtualKey = (ushort)virtualKey;
                keyDown = new[] { kd };
                keyDownCache[virtualKey] = keyDown;
            }

            injector.InjectKeyboardInput(keyDown);
        }

        public void SendKeyUp(VirtualKey virtualKey)
        {
            IEnumerable<InjectedInputKeyboardInfo> keyUp;
            if (!keyUpCache.TryGetValue(virtualKey, out keyUp))
            {
                var ku = new InjectedInputKeyboardInfo();
                ku.KeyOptions = InjectedInputKeyOptions.KeyUp | InjectedInputKeyOptions.ScanCode;
                ku.ScanCode = (ushort)MapVirtualKeyW((ushort)virtualKey, 0);
                //ku.VirtualKey = (ushort)virtualKey;
                keyUp = new[] { ku };
                keyUpCache[virtualKey] = keyUp;
            }

            injector.InjectKeyboardInput(keyUp);
        }
    }
}