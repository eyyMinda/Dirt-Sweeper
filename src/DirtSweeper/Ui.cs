using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DirtSweeper
{
    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(23, 23, 28);
        public static readonly Color Panel = Color.FromArgb(32, 32, 39);
        public static readonly Color Field = Color.FromArgb(44, 44, 53);
        public static readonly Color Hover = Color.FromArgb(55, 55, 66);
        public static readonly Color Text = Color.FromArgb(236, 236, 240);
        public static readonly Color Sub = Color.FromArgb(150, 150, 164);
        public static readonly Color Accent = Color.FromArgb(255, 106, 26);
        public static readonly Color Green = Color.FromArgb(61, 220, 132);
        public static readonly Color Red = Color.FromArgb(255, 92, 92);
        public static readonly Color Blue = Color.FromArgb(80, 160, 255);
        public static readonly Color Yellow = Color.FromArgb(241, 196, 15);

        public static readonly Font Ui = new Font("Segoe UI", 9f);
        public static readonly Font UiBold = new Font("Segoe UI", 9f, FontStyle.Bold);
        public static readonly Font Small = new Font("Segoe UI", 8f);
        public static readonly Font Header = new Font("Segoe UI Semibold", 10.5f);
        public static readonly Font Icons = new Font("Segoe MDL2 Assets", 11f);

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);

        public static void DarkTitleBar(IntPtr hwnd)
        {
            int on = 1;
            if (DwmSetWindowAttribute(hwnd, 20, ref on, 4) != 0) DwmSetWindowAttribute(hwnd, 19, ref on, 4);
        }

        public static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    static class Glyph
    {
        // Segoe MDL2 Assets
        public const string Play = "", Stop = "", Stopwatch = "", Eye = "",
            Settings = "", Close = "", Game = "";
    }

    /// <summary>
    /// The in-game HUD bar: owner-drawn, never takes focus from the game, and lets the mouse
    /// pass through while the app is moving it (so spots under the bar still get hovered).
    /// </summary>
    sealed class Hud : Form
    {
        public sealed class Button
        {
            public string Glyph, Hint;
            public Action Click;
            public bool Visible = true, Active;
            public Color ActiveColor = Theme.Accent;
            public Rectangle Rect;
        }

        public readonly List<Button> Buttons = new List<Button>();
        public string Title = "", Detail = "";
        public Color Dot = Theme.Sub;
        public event Action Moved;
        public event Action<int> HotKey;

        readonly float k;
        int hover = -1;
        bool dragging, clickThrough;
        Point dragOffset;

        public Hud()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            BackColor = Theme.Bg;
            Opacity = 0.94;
            Text = "Dirt Sweeper";
            using (var g = CreateGraphics()) k = g.DpiX / 96f;
            Size = new Size(S(440), S(46));
            using (var path = Theme.Rounded(new RectangleF(0, 0, Width, Height), S(10)))
                Region = new Region(path);
        }

        int S(float v) => (int)Math.Round(v * k);

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x08 | 0x08000000; // tool window, topmost, no-activate
                return cp;
            }
        }

        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);

        public void SetClickThrough(bool on)
        {
            if (on == clickThrough || !IsHandleCreated) return;
            clickThrough = on;
            int ex = GetWindowLong(Handle, -20);
            SetWindowLong(Handle, -20, on ? ex | 0x20 | 0x80000 : ex & ~0x20);
            if (on) { hover = -1; dragging = false; }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x21) { m.Result = (IntPtr)3; return; }               // WM_MOUSEACTIVATE -> MA_NOACTIVATE
            if (m.Msg == 0x312) { HotKey?.Invoke(m.WParam.ToInt32()); return; } // WM_HOTKEY
            base.WndProc(ref m);
        }

        void LayoutButtons()
        {
            int size = S(34), gap = S(2), x = Width - S(6);
            for (int i = Buttons.Count - 1; i >= 0; i--)
            {
                var b = Buttons[i];
                if (!b.Visible) { b.Rect = Rectangle.Empty; continue; }
                x -= size;
                b.Rect = new Rectangle(x, (Height - size) / 2, size, size);
                x -= gap;
            }
        }

        int TextRight()
        {
            int r = Width;
            foreach (var b in Buttons) if (b.Visible) r = Math.Min(r, b.Rect.Left);
            return r - S(6);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            LayoutButtons();
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(Color.FromArgb(70, 70, 82)))
            using (var path = Theme.Rounded(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), S(10)))
                g.DrawPath(pen, path);
            using (var dot = new SolidBrush(Dot))
                g.FillEllipse(dot, S(12), Height / 2f - S(4), S(8), S(8));

            int tx = S(28), tw = TextRight() - tx;
            var flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            TextRenderer.DrawText(g, Title, Theme.UiBold, new Rectangle(tx, S(5), tw, S(18)), Theme.Text, flags);
            string detail = hover >= 0 ? Buttons[hover].Hint : Detail;
            TextRenderer.DrawText(g, detail, Theme.Small, new Rectangle(tx, S(23), tw, S(17)), Theme.Sub, flags);

            for (int i = 0; i < Buttons.Count; i++)
            {
                var b = Buttons[i];
                if (!b.Visible) continue;
                if (i == hover || b.Active)
                    using (var bg = new SolidBrush(b.Active ? Color.FromArgb(60, b.ActiveColor) : Theme.Hover))
                    using (var path = Theme.Rounded(b.Rect, S(6)))
                        g.FillPath(bg, path);
                TextRenderer.DrawText(g, b.Glyph, Theme.Icons, b.Rect, b.Active ? b.ActiveColor : Theme.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }

        int HitButton(Point p)
        {
            for (int i = 0; i < Buttons.Count; i++)
                if (Buttons[i].Visible && Buttons[i].Rect.Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging)
            {
                var c = Cursor.Position;
                Location = new Point(c.X - dragOffset.X, c.Y - dragOffset.Y);
                return;
            }
            int h = HitButton(e.Location);
            if (h != hover) { hover = h; Invalidate(); }
            Cursor = h >= 0 ? Cursors.Hand : Cursors.SizeAll;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (hover != -1) { hover = -1; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (HitButton(e.Location) < 0) { dragging = true; dragOffset = e.Location; }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (dragging) { dragging = false; Moved?.Invoke(); return; }
            int h = HitButton(e.Location);
            if (h >= 0 && e.Button == MouseButtons.Left) Buttons[h].Click?.Invoke();
        }
    }

    sealed class GuideScene
    {
        public bool ShowStatic;
        public RectangleF Area;
        public List<Point> Path;
        public float Radius;
        public Rectangle TimerBox;
        public Point Button;
    }

    /// <summary>Click-through drawing layer over the game: dig field, sweep spots, timer box, calibration ring.</summary>
    sealed class GuideOverlay : Form
    {
        static readonly Color Key = Color.FromArgb(255, 0, 254);
        public Func<GuideScene> Scene;
        Point? ring;
        float ringRadius;
        string ringLabel;

        public GuideOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Key;
            TransparencyKey = Key;
            DoubleBuffered = true;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x08 | 0x08000000; // layered, click-through, tool, topmost, no-activate
                return cp;
            }
        }

        Rectangle RingRect(Point? c, float r)
        {
            if (c == null) return Rectangle.Empty;
            int pad = 30;
            return new Rectangle((int)(c.Value.X - r) - Left - pad, (int)(c.Value.Y - r) - Top - pad - 20,
                (int)(2 * r) + 2 * pad, (int)(2 * r) + 2 * pad + 20);
        }

        public void SetRing(Point? center, float radius, string label)
        {
            if (center == ring && Math.Abs(radius - ringRadius) < 0.5f && label == ringLabel) return;
            Invalidate(RingRect(ring, ringRadius));
            ring = center; ringRadius = radius; ringLabel = label;
            Invalidate(RingRect(ring, ringRadius));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TranslateTransform(-Left, -Top);
            var s = Scene?.Invoke();
            if (s != null && s.ShowStatic)
            {
                using (var pen = new Pen(Theme.Yellow, 3)) g.DrawRectangle(pen, s.Area.X, s.Area.Y, s.Area.Width, s.Area.Height);
                using (var pen = new Pen(Theme.Blue))
                    foreach (var p in s.Path) g.DrawEllipse(pen, p.X - s.Radius, p.Y - s.Radius, 2 * s.Radius, 2 * s.Radius);
                if (s.Path.Count > 1)
                    using (var pen = new Pen(Theme.Accent)) g.DrawLines(pen, s.Path.ToArray());
                using (var b = new SolidBrush(Theme.Accent))
                    foreach (var p in s.Path) g.FillRectangle(b, p.X - 3, p.Y - 3, 6, 6);
                using (var pen = new Pen(Theme.Red, 2))
                {
                    var t = s.TimerBox;
                    g.DrawRectangle(pen, t.X - 2, t.Y - 2, t.Width + 3, t.Height + 3);
                    g.DrawLine(pen, s.Button.X - 14, s.Button.Y, s.Button.X + 14, s.Button.Y);
                    g.DrawLine(pen, s.Button.X, s.Button.Y - 14, s.Button.X, s.Button.Y + 14);
                }
            }
            if (ring != null)
            {
                var c = ring.Value;
                using (var pen = new Pen(Theme.Green, 2))
                    g.DrawEllipse(pen, c.X - ringRadius, c.Y - ringRadius, 2 * ringRadius, 2 * ringRadius);
                if (!string.IsNullOrEmpty(ringLabel))
                {
                    // no anti-aliasing: blended edges would pick up the transparency key colour
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                    using (var b = new SolidBrush(Theme.Green))
                        g.DrawString(ringLabel, Theme.Header, b, c.X - 30, c.Y - ringRadius - 26);
                }
            }
        }
    }
}
