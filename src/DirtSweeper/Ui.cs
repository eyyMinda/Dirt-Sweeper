using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace DirtSweeper
{
    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(22, 22, 27);
        public static readonly Color Panel = Color.FromArgb(31, 31, 38);
        public static readonly Color Field = Color.FromArgb(43, 43, 52);
        public static readonly Color Hover = Color.FromArgb(56, 56, 68);
        public static readonly Color Border = Color.FromArgb(64, 64, 78);
        public static readonly Color Text = Color.FromArgb(236, 236, 240);
        public static readonly Color Sub = Color.FromArgb(146, 146, 162);
        public static readonly Color Accent = Color.FromArgb(255, 106, 26);
        public static readonly Color Green = Color.FromArgb(61, 220, 132);
        public static readonly Color Red = Color.FromArgb(255, 92, 92);
        public static readonly Color Blue = Color.FromArgb(80, 160, 255);
        public static readonly Color Yellow = Color.FromArgb(241, 196, 15);

        public static readonly Font Ui = new Font("Segoe UI", 9f);
        public static readonly Font UiBold = new Font("Segoe UI Semibold", 9f);
        public static readonly Font Small = new Font("Segoe UI", 8f);
        public static readonly Font Section = new Font("Segoe UI Semibold", 7.5f);
        public static readonly Font Value = new Font("Segoe UI Semibold", 9.5f);
        public static readonly Font Mono = new Font("Consolas", 8f);
        public static readonly Font Icons = new Font("Segoe MDL2 Assets", 10.5f);
        public static readonly Font IconsSmall = new Font("Segoe MDL2 Assets", 8f);

        public static Color A(Color c, int alpha) => Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), c);

        public static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Fill(Graphics g, RectangleF r, float radius, Color c)
        {
            using (var b = new SolidBrush(c)) using (var p = Rounded(r, radius)) g.FillPath(b, p);
        }

        public static void Stroke(Graphics g, RectangleF r, float radius, Color c, float width = 1)
        {
            using (var pen = new Pen(c, width)) using (var p = Rounded(r, radius)) g.DrawPath(pen, p);
        }

        /// <summary>Soft drop shadow made of stacked translucent rounded rects.</summary>
        public static void Shadow(Graphics g, RectangleF r, float radius, float spread)
        {
            int steps = Math.Max(1, (int)spread);
            for (int i = steps; i >= 1; i--)
            {
                var rr = RectangleF.Inflate(r, i, i);
                rr.Offset(0, i * 0.35f);
                Fill(g, rr, radius + i, Color.FromArgb((int)(26f * (1 - (i - 1f) / steps) / steps * 2.2f), 0, 0, 0));
            }
        }

        static readonly StringFormat one = new StringFormat(StringFormatFlags.NoWrap)
        {
            Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Center,
        };
        static readonly StringFormat center = new StringFormat(StringFormatFlags.NoWrap)
        {
            Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center,
        };
        static readonly StringFormat right = new StringFormat(StringFormatFlags.NoWrap)
        {
            Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter,
        };

        public static void Draw(Graphics g, string s, Font f, Color c, RectangleF r, StringAlignment align = StringAlignment.Near)
        {
            using (var b = new SolidBrush(c))
                g.DrawString(s ?? "", f, b, r, align == StringAlignment.Center ? center : align == StringAlignment.Far ? right : one);
        }

        static readonly Bitmap measureBmp = new Bitmap(1, 1);
        public static readonly Graphics Measurer = MakeMeasurer();
        static Graphics MakeMeasurer()
        {
            var g = Graphics.FromImage(measureBmp);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            return g;
        }

        public static SizeF Measure(string s, Font f, float width = 0) =>
            width > 0 ? Measurer.MeasureString(s ?? "", f, (int)width) : Measurer.MeasureString(s ?? "", f);
    }

    static class Glyph
    {
        // Segoe MDL2 Assets
        public const string Play = "", Stop = "", Stopwatch = "", Eye = "",
            Settings = "", Close = "", Game = "", Minus = "", Plus = "",
            Up = "", Down = "";
    }

    /// <summary>
    /// A borderless, topmost, never-activating window drawn as one premultiplied bitmap, so it gets
    /// anti-aliased edges and real transparency. Pixels with zero alpha let the mouse through.
    /// </summary>
    abstract class LayeredWindow : Form
    {
        protected readonly float k;
        bool clickThrough;

        protected LayeredWindow(bool clickThrough)
        {
            this.clickThrough = clickThrough;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Text = "Dirt Sweeper";
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) k = g.DpiX / 96f;
        }

        public int S(float v) => (int)Math.Round(v * k);
        public float F(float v) => v * k;

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // layered, tool window, topmost, no-activate (+ click-through)
                cp.ExStyle |= 0x80000 | 0x80 | 0x08 | 0x08000000 | (clickThrough ? 0x20 : 0);
                return cp;
            }
        }

        public bool ClickThrough => clickThrough;

        public void SetClickThrough(bool on)
        {
            if (on == clickThrough) return;
            clickThrough = on;
            if (!IsHandleCreated) return;
            int ex = Native.GetWindowLong(Handle, -20);
            Native.SetWindowLong(Handle, -20, on ? ex | 0x20 : ex & ~0x20);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x21) { m.Result = (IntPtr)3; return; } // WM_MOUSEACTIVATE -> MA_NOACTIVATE
            base.WndProc(ref m);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }

        protected abstract void Render(Graphics g);

        public void Redraw()
        {
            if (!IsHandleCreated || Width < 1 || Height < 1) return;
            using (var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    Render(g);
                }
                Native.PushLayered(Handle, bmp, Location);
            }
        }
    }

    // ======================================================================================
    // HUD bar + settings panel
    // ======================================================================================

    /// <summary>A clickable / scrollable region registered while drawing.</summary>
    sealed class Hit
    {
        public string Id, Hint;
        public RectangleF R;
        public Action Click;
        public Action<int> Wheel;
        public bool Repeat, Drag;
    }

    /// <summary>
    /// Immediate-mode layout for the settings panel: each call draws one row and registers its
    /// hit regions. Runs once without a Graphics to measure the height.
    /// </summary>
    sealed class PanelBuilder
    {
        readonly Hud hud;
        readonly Graphics g;
        readonly List<Hit> hits;
        readonly string hover, pressed;
        public readonly float X, W;
        public float Y;

        public PanelBuilder(Hud hud, Graphics g, List<Hit> hits, float x, float y, float w, string hover, string pressed)
        {
            this.hud = hud; this.g = g; this.hits = hits; X = x; Y = y; W = w; this.hover = hover; this.pressed = pressed;
        }

        float F(float v) => hud.F(v);
        bool Hov(string id) => id != null && id == hover;
        bool Down(string id) => id != null && id == pressed;

        Hit Add(string id, RectangleF r, Action click = null, Action<int> wheel = null, bool repeat = false, string hint = null)
        {
            var h = new Hit { Id = id, R = r, Click = click, Wheel = wheel, Repeat = repeat, Hint = hint };
            hits.Add(h);
            return h;
        }

        public void Space(float v) => Y += F(v);

        public void Tabs(string[] names, int sel, Action<int> pick)
        {
            float h = F(30), gap = F(4), w = (W - gap * (names.Length - 1)) / names.Length;
            if (g != null) Theme.Fill(g, new RectangleF(X - F(3), Y - F(3), W + F(6), h + F(6)), F(9), Theme.Panel);
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var r = new RectangleF(X + i * (w + gap), Y, w, h);
                string id = "tab" + i;
                Add(id, r, () => pick(idx));
                if (g == null) continue;
                bool on = i == sel;
                if (on) Theme.Fill(g, r, F(7), Theme.Field);
                else if (Hov(id)) Theme.Fill(g, r, F(7), Theme.A(Theme.Field, 150));
                Theme.Draw(g, names[i], on ? Theme.UiBold : Theme.Ui, on ? Theme.Text : Theme.Sub, r, StringAlignment.Center);
                if (on)
                    using (var b = new SolidBrush(Theme.Accent))
                        g.FillRectangle(b, r.X + r.Width / 2 - F(10), r.Bottom - F(3), F(20), F(2));
            }
            Y += h + F(8);
        }

        public void Section(string title)
        {
            Y += F(8);
            if (g != null)
            {
                Theme.Draw(g, title.ToUpperInvariant(), Theme.Section, Theme.A(Theme.Accent, 230), new RectangleF(X, Y, W, F(16)));
                float tw = Theme.Measure(title.ToUpperInvariant(), Theme.Section).Width;
                using (var pen = new Pen(Theme.A(Theme.Border, 160)))
                    g.DrawLine(pen, X + tw + F(4), Y + F(8.5f), X + W, Y + F(8.5f));
            }
            Y += F(20);
        }

        public void Text(string text, Color? color = null, Font font = null)
        {
            font = font ?? Theme.Small;
            var size = Theme.Measure(text, font, W);
            if (g != null)
                using (var b = new SolidBrush(color ?? Theme.Sub))
                    g.DrawString(text, font, b, new RectangleF(X, Y, W, size.Height + 2));
            Y += size.Height + F(4);
        }

        public void Info(string label, string value, Color? valueColor = null)
        {
            float h = F(22);
            if (g != null)
            {
                Theme.Draw(g, label, Theme.Ui, Theme.Sub, new RectangleF(X, Y, W * 0.45f, h));
                Theme.Draw(g, value, Theme.Ui, valueColor ?? Theme.Text, new RectangleF(X + W * 0.4f, Y, W * 0.6f, h), StringAlignment.Far);
            }
            Y += h;
        }

        /// <summary>Label on the left, [−] value [+] on the right. Click, hold, or scroll to change.</summary>
        public void Stepper(string id, string label, string value, Action<int> step, string hint, string note = null)
        {
            float h = F(32), bw = F(26), vw = F(74), right = X + W;
            var plus = new RectangleF(right - bw, Y + (h - bw) / 2, bw, bw);
            var box = new RectangleF(plus.X - vw - F(3), plus.Y, vw, bw);
            var minus = new RectangleF(box.X - bw - F(3), plus.Y, bw, bw);
            var row = new RectangleF(X, Y, W, h);
            // the buttons click and repeat while held; the whole row scrolls (buttons first so they win)
            Add(id + "-", minus, () => step(-1), step, true, hint);
            Add(id + "+", plus, () => step(1), step, true, hint);
            Add(id, row, null, step, false, hint + "  ·  scroll or hold ± to change");
            if (g != null)
            {
                bool rowHot = Hov(id) || Hov(id + "-") || Hov(id + "+");
                if (rowHot) Theme.Fill(g, RectangleF.Inflate(row, F(4), 0), F(7), Theme.A(Theme.Field, 110));
                float noteW = note != null ? Theme.Measure(note, Theme.Small).Width + F(6) : 0;
                Theme.Draw(g, label, Theme.Ui, Theme.Text, new RectangleF(X, Y, minus.X - X - noteW - F(4), h));
                if (note != null)
                    Theme.Draw(g, note, Theme.Small, Theme.Sub, new RectangleF(minus.X - noteW - F(4), Y, noteW, h), StringAlignment.Far);
                Theme.Fill(g, box, F(6), Theme.Field);
                Theme.Draw(g, value, Theme.Value, Theme.Text, box, StringAlignment.Center);
                RoundButton(minus, Glyph.Minus, id + "-");
                RoundButton(plus, Glyph.Plus, id + "+");
            }
            Y += h;
        }

        void RoundButton(RectangleF r, string glyph, string id)
        {
            var c = Down(id) ? Theme.A(Theme.Accent, 120) : Hov(id) ? Theme.Hover : Theme.Field;
            Theme.Fill(g, r, F(6), c);
            Theme.Draw(g, glyph, Theme.IconsSmall, Hov(id) ? Theme.Text : Theme.Sub, r, StringAlignment.Center);
        }

        public void Toggle(string id, string label, bool on, Action flip, string hint)
        {
            float h = F(28), sw = F(34), sh = F(18);
            var row = new RectangleF(X, Y, W, h);
            Add(id, row, flip, null, false, hint);
            if (g != null)
            {
                if (Hov(id)) Theme.Fill(g, RectangleF.Inflate(row, F(4), 0), F(7), Theme.A(Theme.Field, 110));
                Theme.Draw(g, label, Theme.Ui, Theme.Text, new RectangleF(X, Y, W - sw - F(8), h));
                var track = new RectangleF(X + W - sw, Y + (h - sh) / 2, sw, sh);
                Theme.Fill(g, track, sh / 2, on ? Theme.Green : Theme.Hover);
                float kd = sh - F(4), kx = on ? track.Right - kd - F(2) : track.X + F(2);
                using (var b = new SolidBrush(on ? Color.White : Theme.Sub)) g.FillEllipse(b, kx, track.Y + F(2), kd, kd);
            }
            Y += h;
        }

        public struct Btn
        {
            public string Id, Text, Hint;
            public Action Click;
            public bool Primary, Active;
            public Color ActiveColor;
        }

        public static Btn B(string id, string text, Action click, string hint, bool primary = false, bool active = false, Color? activeColor = null) =>
            new Btn { Id = id, Text = text, Click = click, Hint = hint, Primary = primary, Active = active, ActiveColor = activeColor ?? Theme.Green };

        public void Buttons(params Btn[] items)
        {
            float h = F(32), gap = F(6), w = (W - gap * (items.Length - 1)) / items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                var r = new RectangleF(X + i * (w + gap), Y, w, h);
                Add(it.Id, r, it.Click, null, false, it.Hint);
                if (g == null) continue;
                bool hov = Hov(it.Id), down = Down(it.Id);
                Color bg, fg;
                if (it.Active) { bg = Theme.A(it.ActiveColor, hov ? 90 : 60); fg = it.ActiveColor; }
                else if (it.Primary) { bg = hov ? Color.FromArgb(255, 130, 60) : Theme.Accent; fg = Color.FromArgb(20, 12, 6); }
                else { bg = hov ? Theme.Hover : Theme.Field; fg = Theme.Text; }
                if (down) r.Offset(0, F(1));
                Theme.Fill(g, r, F(7), bg);
                if (it.Active) Theme.Stroke(g, r, F(7), Theme.A(it.ActiveColor, 160));
                Theme.Draw(g, it.Text, Theme.UiBold, fg, r, StringAlignment.Center);
            }
            Y += h + F(6);
        }

        /// <summary>A row of small toggle chips, e.g. which timer levels to play.</summary>
        public void Chips(string id, string label, int count, Func<int, bool> on, Action<int> toggle, string hint)
        {
            float h = F(32), cw = F(28), ch = F(26), gap = F(4);
            if (g != null) Theme.Draw(g, label, Theme.Ui, Theme.Text, new RectangleF(X, Y, W - count * (cw + gap), h));
            for (int i = count; i >= 1; i--)
            {
                int n = i;
                var r = new RectangleF(X + W - (count - i + 1) * cw - (count - i) * gap, Y + (h - ch) / 2, cw, ch);
                string cid = id + i;
                Add(cid, r, () => toggle(n), null, false, hint);
                if (g == null) continue;
                bool isOn = on(n), hov = Hov(cid);
                Theme.Fill(g, r, F(6), isOn ? (hov ? Color.FromArgb(255, 130, 60) : Theme.Accent) : hov ? Theme.Hover : Theme.Field);
                Theme.Draw(g, n.ToString(), Theme.UiBold, isOn ? Color.FromArgb(20, 12, 6) : Theme.Sub, r, StringAlignment.Center);
            }
            Y += h;
        }

        public void Log(IList<string> lines, int max)
        {
            float lh = F(15), h = lh * max + F(10);
            var r = new RectangleF(X, Y, W, h);
            if (g != null)
            {
                Theme.Fill(g, r, F(6), Theme.Panel);
                int start = Math.Max(0, lines.Count - max);
                for (int i = start; i < lines.Count; i++)
                    Theme.Draw(g, lines[i], Theme.Mono, Theme.Sub, new RectangleF(X + F(8), Y + F(5) + (i - start) * lh, W - F(16), lh));
                if (lines.Count == 0) Theme.Draw(g, "nothing yet", Theme.Mono, Theme.A(Theme.Sub, 120), new RectangleF(X + F(8), Y + F(5), W, lh));
            }
            Y += h + F(4);
        }
    }

    /// <summary>
    /// The in-game HUD: a status bar with buttons, plus a settings panel that unfolds above or
    /// below it. It never takes focus from the game, so every setting can be changed while
    /// watching the effect live.
    /// </summary>
    sealed class Hud : LayeredWindow
    {
        public sealed class Button
        {
            public string Glyph, Hint;
            public Action Click;
            public bool Visible = true, Active;
            public Color ActiveColor = Theme.Accent;
        }

        public readonly List<Button> Buttons = new List<Button>();
        public string Title = "", Detail = "";
        public Color Dot = Theme.Sub;
        public bool PanelOpen, Busy;
        public Action<PanelBuilder> BuildPanel;
        public event Action Moved;
        public event Action<int> HotKey;

        readonly int m;  // transparent margin that holds the shadow
        readonly float barW, barH, gap, pad;
        Point barPos;
        Rectangle screen;
        bool panelUp;
        RectangleF barRect, panelRect;
        List<Hit> hits = new List<Hit>();
        string hover, pressed;
        bool dragging;
        Point dragOffset;
        readonly Timer repeat = new Timer();
        Hit repeatHit;

        public Hud() : base(false)
        {
            m = S(10);
            barW = F(470); barH = F(48); gap = F(6); pad = F(14);
            repeat.Tick += (_, __) =>
            {
                repeat.Interval = Math.Max(25, repeat.Interval - 15);
                if (repeatHit != null && hover == repeatHit.Id) { repeatHit.Click?.Invoke(); Redraw(); }
            };
        }

        /// <summary>Top-left of the bar on screen (what gets saved as the HUD position).</summary>
        public Point BarPos => barPos;
        public Size BarSize => new Size((int)barW, (int)barH);
        public bool Dragging => dragging;

        protected override void WndProc(ref Message msg)
        {
            if (msg.Msg == 0x312) { HotKey?.Invoke(msg.WParam.ToInt32()); return; }                         // WM_HOTKEY
            if (msg.Msg == 0x20A) { HandleWheel(Cursor.Position, (short)(msg.WParam.ToInt64() >> 16)); return; } // WM_MOUSEWHEEL
            base.WndProc(ref msg);
        }

        float MeasurePanel()
        {
            if (!PanelOpen || BuildPanel == null) return 0;
            var b = new PanelBuilder(this, null, new List<Hit>(), 0, 0, barW - 2 * pad, null, null);
            BuildPanel(b);
            return b.Y + pad * 2;
        }

        /// <summary>Positions the window around the bar, opening the panel towards the roomier side, and redraws.</summary>
        public void Place(Point bar, Rectangle screenBounds)
        {
            barPos = bar;
            screen = screenBounds;
            float ph = MeasurePanel();
            panelUp = bar.Y - screen.Top > screen.Bottom - (bar.Y + barH);
            int w = (int)Math.Ceiling(barW) + 2 * m;
            int h = (int)Math.Ceiling(barH + (ph > 0 ? ph + gap : 0)) + 2 * m;
            int top = panelUp && ph > 0 ? bar.Y - (int)Math.Ceiling(ph + gap) - m : bar.Y - m;
            barRect = new RectangleF(m, panelUp && ph > 0 ? h - m - barH : m, barW, barH);
            panelRect = ph <= 0 ? RectangleF.Empty
                : new RectangleF(m, panelUp ? m : m + barH + gap, barW, ph);
            var bounds = new Rectangle(bar.X - m, top, w, h);
            if (Bounds != bounds) Bounds = bounds;
            Redraw();
        }

        /// <summary>Re-measures and redraws in place (after the content changed).</summary>
        public void Relayout() { if (IsHandleCreated && !screen.IsEmpty) Place(barPos, screen); }

        protected override void Render(Graphics g)
        {
            var newHits = new List<Hit>();

            // ----- panel -----
            if (!panelRect.IsEmpty)
            {
                Theme.Shadow(g, panelRect, F(12), m - 2);
                Theme.Fill(g, panelRect, F(12), Theme.A(Theme.Bg, 247));
                Theme.Stroke(g, RectangleF.Inflate(panelRect, -0.5f, -0.5f), F(12), Theme.Border);
                var b = new PanelBuilder(this, g, newHits, panelRect.X + pad, panelRect.Y + pad, panelRect.Width - 2 * pad, hover, pressed);
                BuildPanel?.Invoke(b);
                if (Busy)
                {
                    Theme.Fill(g, panelRect, F(12), Theme.A(Theme.Bg, 150));
                    newHits.Clear(); // can't click while the app owns the mouse anyway
                }
            }

            // ----- bar -----
            var bar = barRect;
            Theme.Shadow(g, bar, F(12), m - 2);
            Theme.Fill(g, bar, F(12), Theme.A(Theme.Bg, 247));
            Theme.Stroke(g, RectangleF.Inflate(bar, -0.5f, -0.5f), F(12), Theme.Border);

            float bs = F(34), bgap = F(2), x = bar.Right - F(7);
            for (int i = Buttons.Count - 1; i >= 0; i--)
            {
                var btn = Buttons[i];
                if (!btn.Visible) continue;
                x -= bs;
                var r = new RectangleF(x, bar.Y + (barH - bs) / 2, bs, bs);
                x -= bgap;
                string id = "bar" + i;
                newHits.Add(new Hit { Id = id, R = r, Click = btn.Click, Hint = btn.Hint });
                bool hov = hover == id;
                if (btn.Active) Theme.Fill(g, r, F(8), Theme.A(btn.ActiveColor, hov ? 85 : 55));
                else if (hov) Theme.Fill(g, r, F(8), Theme.Hover);
                Theme.Draw(g, btn.Glyph, Theme.Icons, btn.Active ? btn.ActiveColor : hov ? Theme.Text : Theme.A(Theme.Text, 210), r, StringAlignment.Center);
            }

            // status dot with a soft glow
            float dx = bar.X + F(17), dy = bar.Y + barH / 2;
            using (var glow = new SolidBrush(Theme.A(Dot, 50))) g.FillEllipse(glow, dx - F(7), dy - F(7), F(14), F(14));
            using (var dot = new SolidBrush(Dot)) g.FillEllipse(dot, dx - F(4), dy - F(4), F(8), F(8));

            float tx = bar.X + F(32), tw = x - tx - F(4);
            string detail = Detail;
            var hh = hits.Find(h => h.Id == hover);
            if (hh != null && !string.IsNullOrEmpty(hh.Hint)) detail = hh.Hint;
            Theme.Draw(g, Title, Theme.UiBold, Theme.Text, new RectangleF(tx, bar.Y + F(6), tw, F(19)));
            Theme.Draw(g, detail, Theme.Small, Theme.Sub, new RectangleF(tx, bar.Y + F(24), tw, F(18)));

            newHits.Add(new Hit { Id = "drag", R = bar, Drag = true }); // last, so buttons win
            hits = newHits;
        }

        Hit HitAt(Point p)
        {
            foreach (var h in hits)
                if (h.R.Contains(p) && (h.Click != null || h.Drag || h.Wheel != null)) return h;
            return null;
        }

        public bool Contains(Point screenPt)
        {
            if (!Visible) return false;
            var p = new Point(screenPt.X - Left, screenPt.Y - Top);
            return barRect.Contains(p) || panelRect.Contains(p);
        }

        /// <summary>Scroll over a stepper changes it. Returns true when the wheel was over the HUD.</summary>
        public bool HandleWheel(Point screenPt, int delta)
        {
            if (!Contains(screenPt)) return false;
            if (ClickThrough) return false;
            var p = new Point(screenPt.X - Left, screenPt.Y - Top);
            Hit target = null;
            foreach (var h in hits) if (h.Wheel != null && h.R.Contains(p)) { target = h; break; }
            if (target != null)
            {
                int notches = Math.Max(1, Math.Abs(delta) / 120);
                for (int i = 0; i < notches; i++) target.Wheel(Math.Sign(delta));
                Redraw();
            }
            return true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging)
            {
                var c = Cursor.Position;
                var bar = new Point(c.X - dragOffset.X, c.Y - dragOffset.Y);
                if (bar != barPos) Place(bar, screen);
                return;
            }
            var h = HitAt(e.Location);
            string id = h?.Id;
            Cursor = h == null ? Cursors.Default : h.Drag ? Cursors.SizeAll : h.Click != null ? Cursors.Hand : Cursors.Default;
            if (id != hover) { hover = id; Redraw(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (dragging) return;
            if (hover != null || pressed != null) { hover = null; pressed = null; repeat.Stop(); Redraw(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            var h = HitAt(e.Location);
            if (h == null) return;
            if (h.Drag)
            {
                dragging = true;
                dragOffset = new Point(Cursor.Position.X - barPos.X, Cursor.Position.Y - barPos.Y);
                return;
            }
            pressed = h.Id;
            if (h.Repeat)
            {
                h.Click?.Invoke();
                repeatHit = h;
                repeat.Interval = 380;
                repeat.Start();
            }
            Redraw();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            repeat.Stop();
            if (dragging) { dragging = false; Moved?.Invoke(); return; }
            var h = HitAt(e.Location);
            string was = pressed;
            pressed = null;
            repeatHit = null;
            if (h != null && h.Id == was && !h.Repeat && e.Button == MouseButtons.Left) h.Click?.Invoke();
            if (IsHandleCreated && !IsDisposed) Place(barPos, screen); // content may have changed size
        }
    }

    // ======================================================================================
    // Guides: dig field, sweep spots, timer box; drag the field's edges in edit mode
    // ======================================================================================

    sealed class GuideScene
    {
        public bool ShowStatic, Editing;
        public RectangleF Area;
        public List<Point> Path;
        public float Radius;
        public Rectangle TimerBox;
        public Point Button;
        public string AreaLabel;
    }

    sealed class GuideOverlay : LayeredWindow
    {
        public Func<GuideScene> Scene;
        /// <summary>New field rectangle in screen pixels while an edge is being dragged.</summary>
        public Action<RectangleF> AreaDragged;

        const int L = 1, T = 2, R = 4, B = 8;
        int hoverEdges, dragEdges;
        RectangleF dragStart, lastArea;
        Point dragMouse;

        public GuideOverlay() : base(true) { }

        public bool Editing => !ClickThrough;
        public void SetEditing(bool on) { SetClickThrough(!on); if (!on) { dragEdges = hoverEdges = 0; } }

        protected override void Render(Graphics g)
        {
            var s = Scene?.Invoke();
            if (s == null) return;
            g.TranslateTransform(-Left, -Top);
            lastArea = s.Area;
            var a = s.Area;

            if (s.ShowStatic || s.Editing)
            {
                // spots: translucent discs, so overlaps look darker and any gap would show up clearly
                using (var fill = new SolidBrush(Theme.A(Theme.Blue, s.Editing ? 14 : 22)))
                using (var ring = new Pen(Theme.A(Theme.Blue, 110), 1.2f))
                    foreach (var p in s.Path)
                    {
                        g.FillEllipse(fill, p.X - s.Radius, p.Y - s.Radius, 2 * s.Radius, 2 * s.Radius);
                        g.DrawEllipse(ring, p.X - s.Radius, p.Y - s.Radius, 2 * s.Radius, 2 * s.Radius);
                    }
                if (s.Path.Count > 1)
                    using (var pen = new Pen(Theme.A(Theme.Accent, 150), 1.5f) { LineJoin = LineJoin.Round })
                        g.DrawLines(pen, s.Path.ToArray());
                using (var b = new SolidBrush(Theme.Accent))
                using (var o = new Pen(Color.FromArgb(200, 0, 0, 0), 1))
                    foreach (var p in s.Path)
                    {
                        g.FillEllipse(b, p.X - 3.5f, p.Y - 3.5f, 7, 7);
                        g.DrawEllipse(o, p.X - 3.5f, p.Y - 3.5f, 7, 7);
                    }

                // timer box (drawn outside the box so it never covers the digits) and button crosshair
                using (var pen = new Pen(Theme.Red, 2))
                {
                    var t = s.TimerBox;
                    g.DrawRectangle(pen, t.X - 3, t.Y - 3, t.Width + 5, t.Height + 5);
                    g.DrawLine(pen, s.Button.X - 14, s.Button.Y, s.Button.X - 4, s.Button.Y);
                    g.DrawLine(pen, s.Button.X + 4, s.Button.Y, s.Button.X + 14, s.Button.Y);
                    g.DrawLine(pen, s.Button.X, s.Button.Y - 14, s.Button.X, s.Button.Y - 4);
                    g.DrawLine(pen, s.Button.X, s.Button.Y + 4, s.Button.X, s.Button.Y + 14);
                }
            }

            // field
            if (s.Editing)
            {
                using (var b = new SolidBrush(Theme.A(Theme.Yellow, 18))) g.FillRectangle(b, a);
                // an almost invisible band around the edges so they can be grabbed from outside too
                using (var band = new Pen(Color.FromArgb(2, 0, 0, 0), 2 * Grab)) g.DrawRectangle(band, a.X, a.Y, a.Width, a.Height);
            }
            using (var pen = new Pen(Theme.A(Theme.Yellow, s.Editing ? 255 : 200), s.Editing ? 2.5f : 2f))
                g.DrawRectangle(pen, a.X, a.Y, a.Width, a.Height);
            if (s.Editing)
            {
                foreach (int e in new[] { L, R, T, B, L | T, R | T, L | B, R | B })
                {
                    var c = HandlePoint(a, e);
                    bool hot = (dragEdges != 0 ? dragEdges : hoverEdges) == e;
                    float hs = F(hot ? 8 : 6);
                    var hr = new RectangleF(c.X - hs, c.Y - hs, 2 * hs, 2 * hs);
                    Theme.Fill(g, hr, F(3), hot ? Theme.Yellow : Color.White);
                    Theme.Stroke(g, hr, F(3), Color.FromArgb(220, 0, 0, 0));
                }
                if (!string.IsNullOrEmpty(s.AreaLabel))
                {
                    var size = Theme.Measure(s.AreaLabel, Theme.UiBold);
                    var lr = new RectangleF(a.X + a.Width / 2 - size.Width / 2 - F(10), a.Y + a.Height / 2 - F(14), size.Width + F(20), F(28));
                    Theme.Fill(g, lr, F(8), Theme.A(Theme.Bg, 225));
                    Theme.Draw(g, s.AreaLabel, Theme.UiBold, Theme.Yellow, lr, StringAlignment.Center);
                }
            }
        }

        float Grab => F(14);

        static PointF HandlePoint(RectangleF a, int e)
        {
            float x = (e & L) != 0 ? a.Left : (e & R) != 0 ? a.Right : a.X + a.Width / 2;
            float y = (e & T) != 0 ? a.Top : (e & B) != 0 ? a.Bottom : a.Y + a.Height / 2;
            return new PointF(x, y);
        }

        int EdgesAt(Point screenPt)
        {
            var a = lastArea;
            float x = screenPt.X, y = screenPt.Y, gr = Grab;
            bool inY = y >= a.Top - gr && y <= a.Bottom + gr, inX = x >= a.Left - gr && x <= a.Right + gr;
            int e = 0;
            if (inY && Math.Abs(x - a.Left) <= gr) e |= L;
            else if (inY && Math.Abs(x - a.Right) <= gr) e |= R;
            if (inX && Math.Abs(y - a.Top) <= gr) e |= T;
            else if (inX && Math.Abs(y - a.Bottom) <= gr) e |= B;
            return e;
        }

        static Cursor CursorFor(int e)
        {
            if (e == (L | T) || e == (R | B)) return Cursors.SizeNWSE;
            if (e == (R | T) || e == (L | B)) return Cursors.SizeNESW;
            if ((e & (L | R)) != 0) return Cursors.SizeWE;
            if ((e & (T | B)) != 0) return Cursors.SizeNS;
            return Cursors.Default;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!Editing) return;
            var p = Cursor.Position;
            if (dragEdges != 0)
            {
                float dx = p.X - dragMouse.X, dy = p.Y - dragMouse.Y, min = F(40);
                float l = dragStart.Left, t = dragStart.Top, r = dragStart.Right, b = dragStart.Bottom;
                if ((dragEdges & L) != 0) l = Math.Min(l + dx, r - min);
                if ((dragEdges & R) != 0) r = Math.Max(r + dx, l + min);
                if ((dragEdges & T) != 0) t = Math.Min(t + dy, b - min);
                if ((dragEdges & B) != 0) b = Math.Max(b + dy, t + min);
                AreaDragged?.Invoke(RectangleF.FromLTRB(l, t, r, b));
                return;
            }
            int edges = EdgesAt(p);
            Cursor = CursorFor(edges);
            if (edges != hoverEdges) { hoverEdges = edges; Redraw(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (!Editing || e.Button != MouseButtons.Left) return;
            dragEdges = EdgesAt(Cursor.Position);
            dragStart = lastArea;
            dragMouse = Cursor.Position;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (dragEdges == 0) return;
            dragEdges = 0;
            Redraw();
        }
    }

    /// <summary>
    /// The green calibration ring (or the sweeper's current spot). A small window of its own,
    /// so following the cursor is just a window move, not a full-screen redraw.
    /// </summary>
    sealed class RingWindow : LayeredWindow
    {
        float radius = -1;
        string label;
        Color color = Theme.Green;
        Point at;

        public RingWindow() : base(true) { }

        public void Set(Point? center, float r, string text, Color c)
        {
            if (center == null) { if (Visible) Hide(); return; }
            int pad = S(6), top = text != null ? S(40) : 0;
            float lw = text != null ? Theme.Measure(text, Theme.UiBold).Width + F(26) : 0;
            int w = (int)Math.Ceiling(Math.Max(2 * r + 2 * pad, lw)), h = (int)Math.Ceiling(2 * r + 2 * pad) + top;
            var loc = new Point(center.Value.X - w / 2, center.Value.Y - top - pad - (int)Math.Round(r));
            bool redraw = Math.Abs(r - radius) > 0.2f || text != label || c != color || Width != w || Height != h || !Visible;
            radius = r; label = text; color = c; at = center.Value;
            if (redraw)
            {
                Bounds = new Rectangle(loc, new Size(w, h));
                if (!Visible) Show();
                Redraw();
            }
            else if (Location != loc) Location = loc;
        }

        protected override void Render(Graphics g)
        {
            int pad = S(6), top = label != null ? S(40) : 0;
            float cx = Width / 2f, cy = top + pad + radius;
            using (var b = new SolidBrush(Theme.A(color, 26))) g.FillEllipse(b, cx - radius, cy - radius, 2 * radius, 2 * radius);
            using (var shadow = new Pen(Color.FromArgb(150, 0, 0, 0), 4)) g.DrawEllipse(shadow, cx - radius, cy - radius, 2 * radius, 2 * radius);
            using (var pen = new Pen(color, 2)) g.DrawEllipse(pen, cx - radius, cy - radius, 2 * radius, 2 * radius);
            using (var pen = new Pen(Theme.A(color, 200), 1.5f))
            {
                g.DrawLine(pen, cx - F(6), cy, cx + F(6), cy);
                g.DrawLine(pen, cx, cy - F(6), cx, cy + F(6));
            }
            if (label != null)
            {
                var size = Theme.Measure(label, Theme.UiBold);
                var r = new RectangleF(cx - size.Width / 2 - F(13), F(4), size.Width + F(26), F(28));
                Theme.Fill(g, r, F(14), Theme.A(Theme.Bg, 235));
                Theme.Stroke(g, r, F(14), Theme.A(color, 180));
                Theme.Draw(g, label, Theme.UiBold, color, r, StringAlignment.Center);
            }
        }
    }
}
