using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

namespace DirtSweeper
{
    static class Native
    {
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public uint type; public MOUSEINPUT mi; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct MONITORINFOEX
        {
            public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight; public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inputs, int size);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref POINT p);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr mon, ref MONITORINFOEX mi);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool EnumDisplaySettings(string device, int mode, ref DEVMODE dm);
        [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint ms);

        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern bool GdiFlush();

        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int i, int v);

        public static bool KeyDown(System.Windows.Forms.Keys k) => (GetAsyncKeyState((int)k) & 0x8000) != 0;

        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);

        /// <summary>Puts a topmost window above the other topmost windows without activating it.</summary>
        public static void RaiseTopmost(IntPtr h) => SetWindowPos(h, (IntPtr)(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); // NOSIZE | NOMOVE | NOACTIVATE

        // ---------- per-pixel alpha windows ----------
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLENDFUNCTION { public byte Op, Flags, Alpha, Format; }

        [DllImport("user32.dll")]
        static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dstDc, ref POINT dst, ref SIZE size, IntPtr srcDc,
                                               ref POINT src, int key, ref BLENDFUNCTION blend, int flags);

        /// <summary>Shows a premultiplied 32bpp bitmap as the whole window, with per-pixel transparency.</summary>
        public static void PushLayered(IntPtr hwnd, System.Drawing.Bitmap bmp, Point at)
        {
            IntPtr screen = GetDC(IntPtr.Zero), mem = CreateCompatibleDC(screen);
            IntPtr hbmp = bmp.GetHbitmap(Color.FromArgb(0)), old = SelectObject(mem, hbmp);
            try
            {
                var dst = new POINT { X = at.X, Y = at.Y };
                var src = new POINT();
                var size = new SIZE { cx = bmp.Width, cy = bmp.Height };
                var blend = new BLENDFUNCTION { Op = 0, Flags = 0, Alpha = 255, Format = 1 }; // AC_SRC_OVER, AC_SRC_ALPHA
                UpdateLayeredWindow(hwnd, screen, ref dst, ref size, mem, ref src, 0, ref blend, 2); // ULW_ALPHA
            }
            finally
            {
                SelectObject(mem, old);
                DeleteObject(hbmp);
                DeleteDC(mem);
                ReleaseDC(IntPtr.Zero, screen);
            }
        }

        // ---------- low-level mouse hook ----------
        public delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
        [StructLayout(LayoutKind.Sequential)] public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public IntPtr extra; }
        [DllImport("user32.dll")] public static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr mod, uint thread);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);

        const uint MOVE = 0x0001, LEFTDOWN = 0x0002, LEFTUP = 0x0004, VIRTUALDESK = 0x4000, ABSOLUTE = 0x8000;

        static void Send(uint flags, int dx = 0, int dy = 0)
        {
            var inp = new[] { new INPUT { type = 0, mi = new MOUSEINPUT { dx = dx, dy = dy, dwFlags = flags } } };
            SendInput(1, inp, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>Moves the cursor to a screen pixel, on any monitor.</summary>
        public static void MoveTo(int x, int y)
        {
            int vx = GetSystemMetrics(76), vy = GetSystemMetrics(77), vw = GetSystemMetrics(78), vh = GetSystemMetrics(79);
            Send(MOVE | ABSOLUTE | VIRTUALDESK,
                 (int)Math.Round((x - vx) * 65535.0 / (vw - 1)), (int)Math.Round((y - vy) * 65535.0 / (vh - 1)));
        }

        public static void MouseDown() { Send(LEFTDOWN); }
        public static void MouseUp() { Send(LEFTUP); }

        public static Point CursorPos()
        {
            GetCursorPos(out var p);
            return new Point(p.X, p.Y);
        }

        public static Rectangle ClientScreenRect(IntPtr hwnd)
        {
            GetClientRect(hwnd, out var r);
            var p = new POINT();
            ClientToScreen(hwnd, ref p);
            return new Rectangle(p.X, p.Y, r.Right, r.Bottom);
        }

        /// <summary>Refresh rate (Hz) of the monitor a window is on, or of the primary monitor.</summary>
        public static int RefreshRate(IntPtr hwnd)
        {
            string device = null;
            if (hwnd != IntPtr.Zero)
            {
                var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf(typeof(MONITORINFOEX)) };
                if (GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref mi)) device = mi.szDevice;
            }
            var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
            return EnumDisplaySettings(device, -1, ref dm) && dm.dmDisplayFrequency > 1 ? dm.dmDisplayFrequency : 60;
        }
    }

    static class Clock
    {
        static readonly Stopwatch sw = Stopwatch.StartNew();

        public static double Now => sw.ElapsedTicks / (double)Stopwatch.Frequency;

        /// <summary>Sleeps most of the way, then spins the last ~1.5ms for accurate timing.</summary>
        public static void SleepUntil(double t, CancellationToken ct = default(CancellationToken))
        {
            while (!ct.IsCancellationRequested)
            {
                double rem = t - Now;
                if (rem <= 0) return;
                if (rem > 0.002) Thread.Sleep((int)((rem - 0.0015) * 1000));
                else Thread.SpinWait(20);
            }
        }
    }
}

namespace DirtSweeper
{
    /// <summary>
    /// Global mouse hook. Only installed while the app needs it (calibrating, editing, panel open),
    /// so the wheel can resize the ring over the game without the game ever losing focus.
    /// </summary>
    sealed class MouseHook : System.IDisposable
    {
        readonly Native.HookProc proc;
        System.IntPtr hook;

        /// <summary>Wheel at a screen point with a delta (120 per notch). Return true to swallow it.</summary>
        public System.Func<System.Drawing.Point, int, bool> Wheel;
        /// <summary>Real (not injected) mouse movement.</summary>
        public System.Action<System.Drawing.Point> Move;

        public MouseHook() { proc = Callback; }

        public void Set(bool on)
        {
            if (on && hook == System.IntPtr.Zero)
                hook = Native.SetWindowsHookEx(14, proc, Native.GetModuleHandle(null), 0); // WH_MOUSE_LL
            else if (!on && hook != System.IntPtr.Zero)
            {
                Native.UnhookWindowsHookEx(hook);
                hook = System.IntPtr.Zero;
            }
        }

        System.IntPtr Callback(int code, System.IntPtr wParam, System.IntPtr lParam)
        {
            if (code >= 0)
            {
                var m = System.Runtime.InteropServices.Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);
                var pt = new System.Drawing.Point(m.pt.X, m.pt.Y);
                int msg = wParam.ToInt32();
                if (msg == 0x020A && Wheel != null && Wheel(pt, (short)(m.mouseData >> 16))) return (System.IntPtr)1;
                if (msg == 0x0200 && (m.flags & 1) == 0) Move?.Invoke(pt);
            }
            return Native.CallNextHookEx(hook, code, wParam, lParam);
        }

        public void Dispose() => Set(false);
    }
}
