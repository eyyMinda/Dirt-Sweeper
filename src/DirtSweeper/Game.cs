using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace DirtSweeper
{
    /// <summary>
    /// Maps the game's 1920x1080 layout onto the real window. The UI scales with the
    /// window height; on screens wider than 16:9 the extra width goes to the dig field,
    /// so things in the right-hand panel (timer, button, field's right edge) are
    /// anchored to the right edge.
    /// </summary>
    sealed class GameLayout
    {
        public readonly Rectangle Win;
        public readonly double Scale, Extra;

        public GameLayout(Rectangle win)
        {
            Win = win;
            Scale = win.Height / 1080.0;
            Extra = win.Width - 1920 * Scale;
        }

        public double X(double x) => Win.X + x * Scale;
        public double XRight(double x) => Win.X + x * Scale + Extra;
        public double Y(double y) => Win.Y + y * Scale;
        public double Px(double v) => v * Scale;

        public Point Right(double x, double y) => new Point((int)Math.Round(XRight(x)), (int)Math.Round(Y(y)));

        public PointF ToLayoutRight(Point p) => new PointF((float)((p.X - Win.X - Extra) / Scale), (float)((p.Y - Win.Y) / Scale));

        public Rectangle RightBox(double cx, double cy, double w, double h)
        {
            int pw = Math.Max(1, (int)Math.Round(Px(w))), ph = Math.Max(1, (int)Math.Round(Px(h)));
            var c = Right(cx, cy);
            return new Rectangle(c.X - pw / 2, c.Y - ph / 2, pw, ph);
        }
    }

    sealed class GameState
    {
        public bool Found;
        public IntPtr Hwnd;
        public int RefreshHz;
        public GameLayout Layout;

        public bool InFront => !Found || Native.GetForegroundWindow() == Hwnd;
    }

    /// <summary>Finds the game window and keeps its position, size and refresh rate current.</summary>
    sealed class GameTracker
    {
        IntPtr hwnd;
        public volatile GameState Current;

        public GameTracker() { Current = Fallback(); }

        public void Refresh(string processName)
        {
            if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd)) hwnd = FindWindow(processName);
            if (hwnd == IntPtr.Zero || Native.IsIconic(hwnd)) { Current = Fallback(); return; }
            var rect = Native.ClientScreenRect(hwnd);
            if (rect.Width < 100 || rect.Height < 100) { Current = Fallback(); return; }
            Current = new GameState { Found = true, Hwnd = hwnd, RefreshHz = Native.RefreshRate(hwnd), Layout = new GameLayout(rect) };
        }

        /// <summary>Full path of the game's exe, filled in once the game has been seen running.</summary>
        public volatile string ExePath;

        IntPtr FindWindow(string processName)
        {
            IntPtr found = IntPtr.Zero;
            foreach (var p in Process.GetProcessesByName(processName ?? ""))
            {
                using (p)
                {
                    if (found != IntPtr.Zero || p.MainWindowHandle == IntPtr.Zero) continue;
                    found = p.MainWindowHandle;
                    try { ExePath = p.MainModule.FileName; } catch (Exception) { }
                }
            }
            return found;
        }

        static GameState Fallback() => new GameState
        {
            Found = false,
            RefreshHz = Native.RefreshRate(IntPtr.Zero),
            Layout = new GameLayout(Screen.PrimaryScreen.Bounds),
        };
    }
}
