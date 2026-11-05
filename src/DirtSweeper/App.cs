using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace DirtSweeper
{
    /// <summary>Owns the state and wires the HUD, guides, settings window, sweeper and minigames together.</summary>
    sealed class App : ApplicationContext
    {
        public static readonly Icon AppIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        public const string ShowSignalName = "DirtSweeper.ShowSettings";

        public readonly Settings S = Settings.Load();
        public readonly GameTracker Game = new GameTracker();
        public readonly Sweeper Sweeper;
        readonly TimerGame timerGame = new TimerGame();

        readonly Hud hud = new Hud();
        readonly GuideOverlay guides = new GuideOverlay();
        readonly NotifyIcon tray;
        readonly System.Windows.Forms.Timer tick = new System.Windows.Forms.Timer { Interval = 50 };
        SettingsForm settings;

        readonly Hud.Button btnSweep, btnTimer, btnGuides, btnLaunch;
        bool guidesOn, calibrating, hudShown, guidesShown;
        string pickWhat;
        double pickDeadline, nextGameRefresh, saveAt = double.MaxValue;
        string lastNote = "";
        CancellationTokenSource mgCts;
        bool resumeAfterMinigame;
        List<Point> preview = new List<Point>();
        Rectangle previewFor;
        bool previewDirty = true;
        readonly List<string> logLines = new List<string>();
        readonly EventWaitHandle showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);

        public App()
        {
            Game.Refresh(S.GameProcess);
            Sweeper = new Sweeper(S, Game);

            btnSweep = new Hud.Button { Hint = "F5 · start / stop sweeping", Click = ToggleSweep, ActiveColor = Theme.Green };
            btnTimer = new Hud.Button { Glyph = Glyph.Stopwatch, Hint = "F8 · play the timer game (then click Play)", Click = ToggleTimerGame };
            btnGuides = new Hud.Button { Glyph = Glyph.Eye, Hint = "F6 · show the dig field, spots and timer box", Click = ToggleGuides, ActiveColor = Theme.Blue };
            btnLaunch = new Hud.Button { Glyph = Glyph.Game, Hint = "Launch Dirt Clicker", Click = LaunchGame };
            hud.Buttons.AddRange(new[]
            {
                btnLaunch, btnSweep, btnTimer, btnGuides,
                new Hud.Button { Glyph = Glyph.Settings, Hint = "Settings", Click = ShowSettings },
                new Hud.Button { Glyph = Glyph.Close, Hint = "Exit Dirt Sweeper", Click = Exit },
            });
            hud.Moved += SaveHudPosition;
            hud.HotKey += OnHotKey;
            hud.CreateControl();
            var h = hud.Handle; // create it now so hotkeys can be registered
            for (int i = 0; i < 5; i++)
                if (!Native.RegisterHotKey(h, i + 1, 0x4000, (uint)(Keys.F5 + i)))
                    lastNote = $"F{5 + i} is used by another app, so that hotkey won't work";

            guides.Scene = BuildScene;

            var menu = new ContextMenuStrip();
            menu.Items.Add("Settings", null, (_, __) => ShowSettings());
            menu.Items.Add("Show / hide HUD  (F9)", null, (_, __) => ToggleHud());
            menu.Items.Add("Exit", null, (_, __) => Exit());
            tray = new NotifyIcon { Icon = AppIcon, Text = "Dirt Sweeper", ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += (_, __) => ShowSettings();

            ThreadPool.RegisterWaitForSingleObject(showSignal, (_, __) => hud.BeginInvoke((MethodInvoker)ShowSettings), null, -1, false);

            tick.Tick += (_, __) => Tick();
            tick.Start();
            Tick();
            if (!Game.Current.Found) ShowSettings(); // first thing people see when the game isn't up yet
            else tray.ShowBalloonTip(4000, "Dirt Sweeper is running",
                "The HUD bar appears over Dirt Clicker. F5 sweeps, F8 plays the timer game. Double-click this icon for settings.",
                ToolTipIcon.None);
        }

        public bool MinigameActive => mgCts != null;
        public int PreviewCount { get { UpdatePreview(); return preview.Count; } }

        // ---------- actions ----------
        public void ToggleSweep()
        {
            if (MinigameActive) return;
            if (Sweeper.Running) { Sweeper.Stop("stopped"); return; }
            calibrating = false;
            lastNote = "";
            pickWhat = null;
            if (settings != null && settings.ContainsFocus) FocusGame();
            Sweeper.Start();
        }

        public void ToggleGuides() { guidesOn = !guidesOn; guides.Invalidate(); }
        public void ToggleCalibrate()
        {
            calibrating = !calibrating;
            if (calibrating && Sweeper.Running) Sweeper.Stop("stopped for calibration");
            if (calibrating && settings != null && settings.ContainsFocus) FocusGame();
        }

        public void ToggleHud() { S.ShowHud = !S.ShowHud; Changed(); settings?.ReloadValues(); }

        public void ToggleTimerGame()
        {
            if (mgCts != null) { mgCts.Cancel(); return; }
            RunMinigame(timerGame);
        }

        void RunMinigame(Minigame mg)
        {
            resumeAfterMinigame = Sweeper.Running;
            Sweeper.Stop();
            calibrating = false;
            lastNote = "";
            if (settings != null && settings.ContainsFocus) FocusGame();
            var cts = mgCts = new CancellationTokenSource();
            var game = Game.Current;
            mg.Result = "";
            new Thread(() =>
            {
                bool ok = false;
                try { ok = mg.Run(S, game, cts.Token); }
                catch (Exception ex) { mg.Result = $"{mg.Name} failed: {ex.Message}"; }
                hud.BeginInvoke((MethodInvoker)(() =>
                {
                    mgCts = null;
                    lastNote = cts.IsCancellationRequested ? $"{mg.Name} cancelled" : mg.Result;
                    if (ok && resumeAfterMinigame && S.ResumeAfter) Sweeper.Start();
                    cts.Dispose();
                }));
            }) { IsBackground = true, Name = mg.Name }.Start();
        }

        public void StartPick(string what)
        {
            pickWhat = what;
            pickDeadline = Clock.Now + 3;
        }

        void FinishPick()
        {
            var lay = Game.Current.Layout;
            var p = lay.ToLayoutRight(Native.CursorPos());
            if (pickWhat == "timer") { S.TimerX = Math.Round(p.X); S.TimerY = Math.Round(p.Y); }
            else { S.ButtonX = Math.Round(p.X); S.ButtonY = Math.Round(p.Y); }
            lastNote = $"{pickWhat} position set to {p.X:0}, {p.Y:0}";
            pickWhat = null;
            Changed();
            settings?.ReloadValues();
        }

        public void LaunchGame()
        {
            string path = Game.ExePath ?? S.GamePath;
            if (Game.Current.Found) { FocusGame(); return; }
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                lastNote = "game exe not found; start it from Steam once so the app can remember it";
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo(path) { WorkingDirectory = Path.GetDirectoryName(path), UseShellExecute = true });
                lastNote = "launching Dirt Clicker…";
            }
            catch (Exception ex) { lastNote = "couldn't launch the game: " + ex.Message; }
        }

        void FocusGame()
        {
            var g = Game.Current;
            if (g.Found) SetForegroundWindow(g.Hwnd);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);

        public void ShowSettings()
        {
            if (settings == null || settings.IsDisposed) settings = new SettingsForm(this);
            settings.Show();
            settings.WindowState = FormWindowState.Normal;
            settings.Activate();
            foreach (var line in logLines) settings.AddLog(line);
            logLines.Clear();
        }

        public void ResetSettings()
        {
            S.SetDefaults();
            Changed();
            settings?.ReloadValues();
        }

        /// <summary>Called whenever a setting changes.</summary>
        public void Changed()
        {
            previewDirty = true;
            saveAt = Clock.Now + 0.5;
            if (guidesShown) guides.Invalidate();
        }

        public void Exit()
        {
            tick.Stop();
            mgCts?.Cancel();
            Sweeper.Quit();
            for (int i = 1; i <= 5; i++) Native.UnregisterHotKey(hud.Handle, i);
            S.Save();
            tray.Visible = false;
            tray.Dispose();
            ExitThread();
        }

        void OnHotKey(int id)
        {
            switch (id)
            {
                case 1: ToggleSweep(); break;
                case 2: ToggleGuides(); break;
                case 3: ToggleCalibrate(); break;
                case 4: ToggleTimerGame(); break;
                case 5: ToggleHud(); break;
            }
        }

        // ---------- HUD position ----------
        void SaveHudPosition()
        {
            var w = Game.Current.Layout.Win;
            S.HudFx = (hud.Left - w.X) / (double)w.Width;
            S.HudFy = (hud.Top - w.Y) / (double)w.Height;
            Changed();
        }

        Point HudLocation()
        {
            var w = Game.Current.Layout.Win;
            int x = w.X + (int)Math.Round(S.HudFx * w.Width), y = w.Y + (int)Math.Round(S.HudFy * w.Height);
            var screen = Screen.FromRectangle(w).Bounds;
            x = Math.Max(screen.Left, Math.Min(x, screen.Right - hud.Width));
            y = Math.Max(screen.Top, Math.Min(y, screen.Bottom - hud.Height));
            return new Point(x, y);
        }

        // ---------- guides ----------
        void UpdatePreview()
        {
            var lay = Game.Current.Layout;
            if (!previewDirty && previewFor == lay.Win) return;
            preview = SweepPath.Build(S, lay, null);
            previewFor = lay.Win;
            previewDirty = false;
            if (guidesShown) guides.Invalidate();
        }

        GuideScene BuildScene()
        {
            var lay = Game.Current.Layout;
            UpdatePreview();
            return new GuideScene
            {
                ShowStatic = guidesOn,
                Area = SweepPath.Area(S, lay),
                Path = preview,
                Radius = (float)(SweepPath.CircleDiameter(S, lay) / 2),
                TimerBox = lay.RightBox(S.TimerX, S.TimerY, S.TimerW, S.TimerH),
                Button = lay.Right(S.ButtonX, S.ButtonY),
            };
        }

        // ---------- main loop (UI thread, 20x a second) ----------
        void Tick()
        {
            double now = Clock.Now;
            if (now >= nextGameRefresh)
            {
                Game.Refresh(S.GameProcess);
                if (Game.ExePath != null && Game.ExePath != S.GamePath) { S.GamePath = Game.ExePath; Changed(); }
                nextGameRefresh = now + 1;
            }
            var g = Game.Current;
            if (pickWhat != null && now >= pickDeadline) FinishPick();

            while (timerGame.LogLines.TryDequeue(out var line))
            {
                if (settings != null && !settings.IsDisposed) settings.AddLog(line);
                else logLines.Add(line);
            }

            // which windows are "ours or the game": the HUD and guides only show over those
            var fg = Native.GetForegroundWindow();
            bool gameContext = !g.Found || fg == g.Hwnd || fg == hud.Handle ||
                               (settings != null && !settings.IsDisposed && settings.Visible && fg == settings.Handle);

            UpdateHud(g, gameContext);
            UpdateGuides(g, gameContext);

            if (settings != null && settings.Visible) settings.RefreshLive();
            if (now >= saveAt) { S.Save(); saveAt = double.MaxValue; }
        }

        void UpdateHud(GameState g, bool gameContext)
        {
            bool busy = Sweeper.Running || MinigameActive;
            btnLaunch.Visible = !g.Found;
            btnSweep.Glyph = Sweeper.Running ? Glyph.Stop : Glyph.Play;
            btnSweep.Active = Sweeper.Running;
            btnTimer.Active = MinigameActive;
            btnGuides.Active = guidesOn;

            if (pickWhat != null)
            {
                hud.Dot = Theme.Blue;
                hud.Title = $"Hover the {(pickWhat == "timer" ? "timer digits" : "red button")}…";
                hud.Detail = $"capturing the position in {Math.Ceiling(pickDeadline - Clock.Now)}";
            }
            else if (MinigameActive)
            {
                hud.Dot = Theme.Accent;
                hud.Title = "Timer game · F8 to cancel";
                hud.Detail = timerGame.Status;
            }
            else if (Sweeper.Running)
            {
                hud.Dot = Theme.Green;
                hud.Title = "Sweeping · F5 to stop";
                hud.Detail = Sweeper.Note == "" ? "starting…" : Sweeper.Note;
            }
            else if (calibrating)
            {
                hud.Dot = Theme.Green;
                hud.Title = "Calibrating · F7 to finish";
                hud.Detail = "match the green ring to the game's hover circle";
            }
            else
            {
                hud.Dot = g.Found ? Theme.Sub : Theme.Yellow;
                hud.Title = g.Found ? "Ready" : "Game not running";
                string note = Sweeper.Note != "" && lastNote == "" ? Sweeper.Note : lastNote;
                hud.Detail = note != "" ? note : g.Found ? "F5 sweep · F8 timer game · F6 guides" : "start Dirt Clicker, or click the controller icon";
            }

            bool show = S.ShowHud && gameContext;
            if (show)
            {
                hud.SetClickThrough(busy);
                var loc = HudLocation();
                if (!hudShown || hud.Location != loc && !hud.Capture) hud.Location = loc;
                if (!hudShown) { hud.Show(); hudShown = true; }
                hud.Invalidate();
            }
            else if (hudShown) { hud.Hide(); hudShown = false; }
        }

        void UpdateGuides(GameState g, bool gameContext)
        {
            bool show = (guidesOn || calibrating || pickWhat != null) && gameContext;
            if (show)
            {
                if (guides.Bounds != g.Layout.Win) { guides.Bounds = g.Layout.Win; guides.Invalidate(); }
                if (!guidesShown) { guides.Show(); guidesShown = true; guides.Invalidate(); }
                float r = (float)(SweepPath.CircleDiameter(S, g.Layout) / 2);
                if (calibrating) guides.SetRing(Native.CursorPos(), r, $"{2 * r:0} px");
                else if (Sweeper.Running && Sweeper.HasPos) guides.SetRing(new Point(Sweeper.PosX, Sweeper.PosY), r, null);
                else guides.SetRing(null, 0, null);
            }
            else if (guidesShown) { guides.Hide(); guidesShown = false; }
        }
    }
}
