using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace DirtSweeper
{
    /// <summary>Dark settings window opened from the HUD's gear button.</summary>
    sealed class SettingsForm : Form
    {
        readonly App app;
        readonly Settings s;
        readonly float k;
        readonly List<Action> reloaders = new List<Action>();
        readonly List<Panel> pages = new List<Panel>();
        readonly List<System.Windows.Forms.Button> navButtons = new List<System.Windows.Forms.Button>();
        bool loading;

        Label lblCircle, lblHold, lblPass, lblGame, lblFps, lblLevels;
        System.Windows.Forms.Button btnStart, btnArm;
        ListBox log;

        public SettingsForm(App app)
        {
            this.app = app;
            s = app.S;
            Text = "Dirt Sweeper";
            Font = Theme.Ui;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            Icon = App.AppIcon;
            using (var g = CreateGraphics()) k = g.DpiX / 96f;

            var nav = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Panel,
                Padding = new Padding(S(8), S(12), S(8), S(8)), Dock = DockStyle.Left, Width = S(150),
            };
            var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(S(16), S(12), S(16), S(12)) };

            AddPage(nav, content, "Sweeper", BuildSweeper());
            AddPage(nav, content, "Timer game", BuildTimer());
            AddPage(nav, content, "General", BuildGeneral());

            var size = Size.Empty;
            foreach (var p in pages)
            {
                var ps = p.GetPreferredSize(Size.Empty);
                size = new Size(Math.Max(size.Width, ps.Width), Math.Max(size.Height, ps.Height));
            }
            Controls.Add(content);
            Controls.Add(nav);
            ClientSize = new Size(nav.Width + size.Width + content.Padding.Horizontal, size.Height + content.Padding.Vertical);
            ShowPage(0);
            RefreshLive();
        }

        int S(float v) => (int)Math.Round(v * k);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.DarkTitleBar(Handle);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            base.OnFormClosing(e);
        }

        // ---------- page plumbing ----------
        void AddPage(FlowLayoutPanel nav, Panel content, string title, Panel page)
        {
            int index = pages.Count;
            var b = new System.Windows.Forms.Button
            {
                Text = title, TextAlign = ContentAlignment.MiddleLeft, FlatStyle = FlatStyle.Flat,
                Width = S(134), Height = S(34), Margin = new Padding(0, 0, 0, S(4)), ForeColor = Theme.Text,
                Padding = new Padding(S(8), 0, 0, 0), Cursor = Cursors.Hand,
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Theme.Hover;
            b.Click += (_, __) => ShowPage(index);
            nav.Controls.Add(b);
            navButtons.Add(b);
            page.Location = new Point(content.Padding.Left, content.Padding.Top);
            page.Visible = false;
            pages.Add(page);
            content.Controls.Add(page);
        }

        void ShowPage(int index)
        {
            for (int i = 0; i < pages.Count; i++)
            {
                pages[i].Visible = i == index;
                navButtons[i].BackColor = i == index ? Theme.Field : Theme.Panel;
                navButtons[i].Font = i == index ? Theme.UiBold : Theme.Ui;
            }
        }

        static FlowLayoutPanel Column() => new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = Padding.Empty, Padding = Padding.Empty,
        };

        FlowLayoutPanel Row(params Control[] items)
        {
            var r = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 0, S(4)),
            };
            r.Controls.AddRange(items);
            return r;
        }

        Label Header(string text, bool first = false) => new Label
        {
            Text = text, Font = Theme.Header, AutoSize = true, ForeColor = Theme.Accent,
            Margin = new Padding(0, first ? 0 : S(14), 0, S(6)),
        };

        Label Caption(string text) => new Label
        {
            Text = text, AutoSize = false, Width = S(150), Height = S(24), TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, S(6), 0),
        };

        Label Note(string text = "", int width = 0) => new Label
        {
            Text = text, AutoSize = true, ForeColor = Theme.Sub, Margin = new Padding(S(4), S(4), 0, 0),
            MaximumSize = new Size(width > 0 ? S(width) : 0, 0),
        };

        Label Hint(string text) => new Label
        {
            Text = text, AutoSize = true, ForeColor = Theme.Sub, Font = Theme.Small,
            MaximumSize = new Size(S(470), 0), Margin = new Padding(0, S(2), 0, S(4)),
        };

        System.Windows.Forms.Button Btn(string text, Action click, bool primary = false, int width = 0)
        {
            var b = new System.Windows.Forms.Button
            {
                Text = text, FlatStyle = FlatStyle.Flat, AutoSize = width == 0, Height = S(28), Cursor = Cursors.Hand,
                BackColor = primary ? Theme.Accent : Theme.Field, ForeColor = primary ? Color.Black : Theme.Text,
                Margin = new Padding(0, 0, S(6), 0), Padding = new Padding(S(6), 0, S(6), 0),
            };
            if (width > 0) b.Width = S(width);
            if (primary) b.Font = Theme.UiBold;
            b.FlatAppearance.BorderColor = primary ? Theme.Accent : Theme.Hover;
            b.Click += (_, __) => click();
            return b;
        }

        NumericUpDown Num(Func<double> get, Action<double> set, double min, double max, int decimals = 0, double inc = 1, int width = 70)
        {
            var n = new NumericUpDown
            {
                Minimum = (decimal)min, Maximum = (decimal)max, DecimalPlaces = decimals, Increment = (decimal)inc,
                Width = S(width), BackColor = Theme.Field, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, S(6), 0), TextAlign = HorizontalAlignment.Right,
            };
            Action load = () => n.Value = (decimal)Math.Min(max, Math.Max(min, get()));
            load();
            reloaders.Add(load);
            n.ValueChanged += (_, __) => { if (!loading) { set((double)n.Value); app.Changed(); } };
            return n;
        }

        CheckBox Check(string text, Func<bool> get, Action<bool> set)
        {
            var c = new CheckBox { Text = text, AutoSize = true, ForeColor = Theme.Text, Margin = new Padding(0, 0, 0, S(4)) };
            Action load = () => c.Checked = get();
            load();
            reloaders.Add(load);
            c.CheckedChanged += (_, __) => { if (!loading) { set(c.Checked); app.Changed(); } };
            return c;
        }

        TextBox Field(Func<string> get, Action<string> set, int width)
        {
            var t = new TextBox
            {
                Width = S(width), BackColor = Theme.Field, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, S(1), S(6), 0),
            };
            Action load = () => t.Text = get();
            load();
            reloaders.Add(load);
            t.TextChanged += (_, __) => { if (!loading) { set(t.Text); app.Changed(); } };
            return t;
        }

        /// <summary>Re-reads every control from the settings (after a pick or a reset).</summary>
        public void ReloadValues()
        {
            loading = true;
            try { foreach (var r in reloaders) r(); }
            finally { loading = false; }
            RefreshLive();
        }

        // ---------- pages ----------
        Panel BuildSweeper()
        {
            var c = Column();
            c.Controls.Add(Header("Hover circle", true));
            c.Controls.Add(Row(Caption("Collision radius (game)"), Num(() => s.Stat, v => s.Stat = v, 1, 9999, 1, 1),
                Note("the number shown on the upgrade")));
            c.Controls.Add(Row(Caption("Circle px per point"), Num(() => s.PxPerStat, v => s.PxPerStat = v, 0.1, 100, 2, 0.1),
                Btn("Calibrate  (F7)", app.ToggleCalibrate)));
            c.Controls.Add(lblCircle = Note());
            c.Controls.Add(Hint("Calibrate shows a green ring around your cursor in the game. Change \"px per point\" until it " +
                                "matches the game's hover circle. After that, only update the collision radius when you buy upgrades."));

            c.Controls.Add(Header("Speed"));
            c.Controls.Add(Row(Caption("Hold on each spot"), Num(() => s.HoldFrames, v => s.HoldFrames = (int)v, 1, 60),
                lblHold = Note()));
            c.Controls.Add(Row(Caption("Spot spacing"), Num(() => s.Spacing, v => s.Spacing = (int)v, 40, 100, 0, 5),
                Note("%   lower = more overlap, safer but slower")));
            c.Controls.Add(lblPass = Note());
            lblPass.ForeColor = Theme.Green;
            c.Controls.Add(Hint("If digging is missed, raise the hold. 1 frame is usually too short, because the game checks hover once per frame."));

            c.Controls.Add(Header("Behaviour"));
            c.Controls.Add(Check("Only sweep while the game is in front", () => s.OnlyWhenFocused, v => s.OnlyWhenFocused = v));
            c.Controls.Add(Check("Stop when I move the mouse myself", () => s.PauseOnMouse, v => s.PauseOnMouse = v));
            c.Controls.Add(Check("Shift the pattern a little every pass", () => s.Shift, v => s.Shift = v));
            c.Controls.Add(Check("Alternate direction every pass", () => s.PingPong, v => s.PingPong = v));
            c.Controls.Add(Check("Sweep in columns instead of rows", () => s.Columns, v => s.Columns = v));

            c.Controls.Add(Header("Dig field  (in 1920×1080 layout)"));
            c.Controls.Add(Row(Caption("Left / Right edge"), Num(() => s.AreaLeft, v => s.AreaLeft = v, 0, 1920),
                Num(() => s.AreaRight, v => s.AreaRight = v, 1, 1920)));
            c.Controls.Add(Row(Caption("Top / Bottom edge"), Num(() => s.AreaTop, v => s.AreaTop = v, 0, 1080),
                Num(() => s.AreaBottom, v => s.AreaBottom = v, 1, 1080)));
            c.Controls.Add(Hint("The right edge is where the shop panel starts. Everything scales to your resolution " +
                                "automatically. Turn on the guides (F6) to see the field and the sweep spots."));

            c.Controls.Add(Row(btnStart = Btn("Start sweeping  (F5)", app.ToggleSweep, true, 200),
                Btn("Guides  (F6)", app.ToggleGuides)));
            btnStart.Margin = new Padding(0, S(14), S(6), 0);
            return c;
        }

        Panel BuildTimer()
        {
            var c = Column();
            c.Controls.Add(Header("Red-button timer game", true));
            c.Controls.Add(Hint("Press F8 (or the button below), then click Play in the game. Every level is played " +
                                "automatically, and sweeping resumes afterwards if it was running."));
            c.Controls.Add(btnArm = Btn("Arm timer game  (F8)", app.ToggleTimerGame, true, 200));
            btnArm.Margin = new Padding(0, S(6), 0, S(4));

            c.Controls.Add(Header("Timing"));
            c.Controls.Add(Row(Caption("Levels (seconds)"), Field(() => s.Levels, v => s.Levels = v, 90),
                lblLevels = Note("add 4,5 when you unlock them")));
            c.Controls.Add(Row(Caption("Click offset"), Num(() => s.OffsetMs, v => s.OffsetMs = (int)v, -500, 500), Note("ms")));
            c.Controls.Add(Hint("Game says you were late by X ms → lower the offset by X. Early → raise it."));
            c.Controls.Add(Check("Resume sweeping afterwards", () => s.ResumeAfter, v => s.ResumeAfter = v));

            c.Controls.Add(Header("Positions  (in 1920×1080 layout)"));
            c.Controls.Add(Row(Caption("Timer digits centre"), Num(() => s.TimerX, v => s.TimerX = v, 0, 1920),
                Num(() => s.TimerY, v => s.TimerY = v, 0, 1080), Btn("Pick", () => app.StartPick("timer"))));
            c.Controls.Add(Row(Caption("Timer box size"), Num(() => s.TimerW, v => s.TimerW = v, 10, 600),
                Num(() => s.TimerH, v => s.TimerH = v, 10, 300)));
            c.Controls.Add(Row(Caption("Red button"), Num(() => s.ButtonX, v => s.ButtonX = v, 0, 1920),
                Num(() => s.ButtonY, v => s.ButtonY = v, 0, 1080), Btn("Pick", () => app.StartPick("button"))));
            c.Controls.Add(Hint("Pick: click it, then hover the item in the game within 3 seconds. The guides (F6) show " +
                                "the red timer box and button crosshair."));

            c.Controls.Add(Header("Detection  (advanced)"));
            c.Controls.Add(Row(Caption("Min countdown run"), Num(() => s.MinRunMs, v => s.MinRunMs = (int)v, 50, 700, 0, 25),
                Note("ms   raise if it clicks too early")));
            c.Controls.Add(Row(Caption("Text brightness"), Num(() => s.TextMin, v => s.TextMin = (int)v, 0, 254, 0, 5),
                Note("lower if it never sees the countdown")));

            c.Controls.Add(Header("Log"));
            c.Controls.Add(log = new ListBox
            {
                Width = S(470), Height = S(96), BackColor = Theme.Panel, ForeColor = Theme.Text,
                BorderStyle = BorderStyle.None, Font = new Font("Consolas", 8.5f), IntegralHeight = false,
            });
            return c;
        }

        Panel BuildGeneral()
        {
            var c = Column();
            c.Controls.Add(Header("Game", true));
            c.Controls.Add(lblGame = Note("", 470));
            lblGame.Margin = new Padding(0, 0, 0, S(8));
            c.Controls.Add(Row(Caption("Game process name"), Field(() => s.GameProcess, v => s.GameProcess = v, 160)));
            c.Controls.Add(Row(Caption("Game FPS"), Num(() => s.FpsOverride, v => s.FpsOverride = (int)v, 0, 1000),
                lblFps = Note()));
            c.Controls.Add(Hint("0 = automatic (your monitor's refresh rate). Set it if the game's FPS is capped lower, " +
                                "so hold times stay right."));
            c.Controls.Add(Row(Btn("Launch game", app.LaunchGame)));

            c.Controls.Add(Header("HUD"));
            c.Controls.Add(Check("Show the HUD bar over the game  (F9)", () => s.ShowHud, v => s.ShowHud = v));
            c.Controls.Add(Hint("Drag the bar anywhere; it remembers its spot. It hides when you switch to another app, " +
                                "and the mouse passes through it while the app is sweeping."));

            c.Controls.Add(Header("Hotkeys"));
            foreach (var line in new[]
                     {
                         "F5    start / stop sweeping", "F6    show / hide guides", "F7    calibration ring",
                         "F8    arm / cancel the timer game", "F9    show / hide the HUD",
                     })
                c.Controls.Add(new Label { Text = line, AutoSize = true, Font = new Font("Consolas", 9f), Margin = new Padding(0, 0, 0, S(2)) });

            c.Controls.Add(Header("App"));
            c.Controls.Add(Row(Btn("Reset all settings", () =>
            {
                if (MessageBox.Show(this, "Reset every setting to its default?", "Dirt Sweeper", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    app.ResetSettings();
            }), Btn("Exit Dirt Sweeper", app.Exit)));
            c.Controls.Add(Hint("Settings file: " + Settings.FilePath));
            c.Controls.Add(Hint("Dirt Sweeper " + Application.ProductVersion));
            return c;
        }

        // ---------- live values ----------
        public void RefreshLive()
        {
            var g = app.Game.Current;
            double fps = app.Sweeper.Fps;
            lblCircle.Text = $"= {SweepPath.CircleDiameter(s, g.Layout):0} px wide circle at your resolution";
            lblHold.Text = $"frames   = {s.HoldFrames * 1000 / fps:0.0} ms at {fps:0} FPS";
            int spots = app.PreviewCount;
            lblPass.Text = $"{spots} spots  →  {spots * app.Sweeper.HoldSeconds:0.00} s per full sweep";
            lblFps.Text = s.FpsOverride > 0 ? "fixed" : $"auto: {g.RefreshHz} Hz";
            lblGame.Text = g.Found
                ? $"● Dirt Clicker is running at {g.Layout.Win.Width}×{g.Layout.Win.Height}, {g.RefreshHz} Hz monitor."
                : "○ Dirt Clicker isn't running. Using the main screen until it starts.";
            lblGame.ForeColor = g.Found ? Theme.Green : Theme.Yellow;
            lblLevels.ForeColor = TimerGame.ParseLevels(s.Levels) == null ? Theme.Red : Theme.Sub;
            lblLevels.Text = TimerGame.ParseLevels(s.Levels) == null ? "not valid, use e.g. 1,2,3" : "add 4,5 when you unlock them";
            btnStart.Text = app.Sweeper.Running ? "Stop sweeping  (F5)" : "Start sweeping  (F5)";
            btnArm.Text = app.MinigameActive ? "Cancel timer game  (F8)" : "Arm timer game  (F8)";
        }

        public void AddLog(string line)
        {
            log.Items.Add(line);
            while (log.Items.Count > 200) log.Items.RemoveAt(0);
            log.TopIndex = log.Items.Count - 1;
        }
    }
}
