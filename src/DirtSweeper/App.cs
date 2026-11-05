using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using static DirtSweeper.PanelBuilder;

namespace DirtSweeper
{
    /// <summary>Owns the state and wires the HUD, settings panel, guides, sweeper and minigames together.</summary>
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
        readonly RingWindow ring = new RingWindow();
        readonly MouseHook hook = new MouseHook();
        readonly NotifyIcon tray;
        readonly System.Windows.Forms.Timer tick = new System.Windows.Forms.Timer { Interval = 50 };

        readonly Hud.Button btnSweep, btnTimer, btnGuides, btnLaunch, btnPanel;
        bool calibrating, editingField, hudShown, guidesShown, guidesDirty = true, hudDirty = true;
        string pickWhat;
        double pickDeadline, nextGameRefresh, saveAt = double.MaxValue, lastHudDraw, confirmResetUntil;
        string lastNote = "";
        CancellationTokenSource mgCts;
        bool resumeAfterMinigame;
        readonly List<string> log = new List<string>();
        ReleaseInfo latest;
        string updateStatus = "";
        bool checkingUpdate, installingUpdate, updateNotified;
        double nextUpdateCheck;
        readonly EventWaitHandle showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);

        public App()
        {
            Game.Refresh(S.GameProcess);
            Sweeper = new Sweeper(S, Game);

            btnSweep = new Hud.Button { Hint = "F5 · start / stop sweeping", Click = ToggleSweep, ActiveColor = Theme.Green };
            btnTimer = new Hud.Button { Glyph = Glyph.Stopwatch, Hint = "F8 · play the timer game (then click Play)", Click = ToggleTimerGame };
            btnGuides = new Hud.Button { Glyph = Glyph.Eye, Hint = "F6 · show the dig field, sweep spots and timer box", Click = ToggleGuides, ActiveColor = Theme.Blue };
            btnLaunch = new Hud.Button { Glyph = Glyph.Game, Hint = "Launch Dirt Clicker", Click = LaunchGame };
            btnPanel = new Hud.Button { Glyph = Glyph.Settings, Hint = "Settings", Click = TogglePanel, ActiveColor = Theme.Accent };
            hud.Buttons.AddRange(new[]
            {
                btnLaunch, btnSweep, btnTimer, btnGuides, btnPanel,
                new Hud.Button { Glyph = Glyph.Close, Hint = "Exit Dirt Sweeper", Click = Exit },
            });
            hud.BuildPanel = BuildPanel;
            hud.Moved += SaveHudPosition;
            hud.HotKey += OnHotKey;
            hud.CreateControl();
            var h = hud.Handle; // create it now so hotkeys can be registered
            for (int i = 0; i < 5; i++)
                if (!Native.RegisterHotKey(h, i + 1, 0x4000, (uint)(Keys.F5 + i)))
                    lastNote = $"F{5 + i} is used by another app, so that hotkey won't work";

            guides.Scene = BuildScene;
            guides.AreaDragged = OnAreaDragged;
            guides.CreateControl();
            ring.CreateControl();
            hook.Wheel = OnHookWheel;
            hook.Move = OnHookMove;

            var menu = new ContextMenuStrip();
            menu.Items.Add("Settings", null, (_, __) => OpenPanel());
            menu.Items.Add("Show / hide HUD  (F9)", null, (_, __) => ToggleHud());
            menu.Items.Add("Exit", null, (_, __) => Exit());
            tray = new NotifyIcon { Icon = AppIcon, Text = "Dirt Sweeper", ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += (_, __) => OpenPanel();

            ThreadPool.RegisterWaitForSingleObject(showSignal, (_, __) => hud.BeginInvoke((MethodInvoker)OpenPanel), null, -1, false);

            Updates.CleanUp(); // the previous exe, if we were just updated
            if (Settings.LoadProblem != null) lastNote = Settings.LoadProblem;
            else if (Settings.MigratedFrom != null) lastNote = "settings moved to " + Settings.FilePath;
            nextUpdateCheck = Clock.Now + 5; // first check shortly after startup, then daily

            tick.Tick += (_, __) => Tick();
            tick.Start();
            if (!Game.Current.Found) hud.PanelOpen = true; // first thing people see when the game isn't up yet
            else tray.ShowBalloonTip(4000, "Dirt Sweeper is running",
                "The HUD bar sits over Dirt Clicker. F5 sweeps, F8 plays the timer game, the gear opens settings.",
                ToolTipIcon.None);
            Tick();
        }

        public bool MinigameActive => mgCts != null;
        bool Busy => Sweeper.Running || MinigameActive;
        static bool Shift => Native.KeyDown(Keys.ShiftKey);
        static bool Ctrl => Native.KeyDown(Keys.ControlKey);

        // ---------- actions ----------
        public void ToggleSweep()
        {
            if (MinigameActive) return;
            if (Sweeper.Running) { Sweeper.Stop("stopped"); return; }
            SetCalibrating(false);
            SetEditing(false);
            lastNote = "";
            pickWhat = null;
            Sweeper.Start();
        }

        public void ToggleGuides() { S.ShowGuides = !S.ShowGuides; Changed(); }
        public void ToggleCalibrate() => SetCalibrating(!calibrating);
        void ToggleEditField() => SetEditing(!editingField);

        void SetCalibrating(bool on)
        {
            if (on == calibrating) return;
            calibrating = on;
            if (on)
            {
                if (Sweeper.Running) Sweeper.Stop("stopped for calibration");
                SetEditing(false);
            }
            hudDirty = true;
        }

        void SetEditing(bool on)
        {
            if (on == editingField) return;
            editingField = on;
            guides.SetEditing(on);
            if (hudShown) Native.RaiseTopmost(hud.Handle);
            if (on)
            {
                if (Sweeper.Running) Sweeper.Stop("stopped to edit the field");
                SetCalibrating(false);
            }
            Changed();
        }

        public void ToggleHud() { S.ShowHud = !S.ShowHud; Changed(); }

        void TogglePanel() { hud.PanelOpen = !hud.PanelOpen; hudDirty = true; hud.Relayout(); }

        void OpenPanel()
        {
            S.ShowHud = true;
            hud.PanelOpen = true;
            FocusGame();
            Changed();
        }

        public void ToggleTimerGame()
        {
            if (mgCts != null) { mgCts.Cancel(); return; }
            RunMinigame(timerGame);
        }

        void RunMinigame(Minigame mg)
        {
            resumeAfterMinigame = Sweeper.Running;
            Sweeper.Stop();
            SetCalibrating(false);
            SetEditing(false);
            lastNote = "";
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
                    hudDirty = true;
                }));
            }) { IsBackground = true, Name = mg.Name }.Start();
        }

        // ---------- updates ----------
        void CheckForUpdates(bool manual)
        {
            if (checkingUpdate || installingUpdate) return;
            checkingUpdate = true;
            if (manual) updateStatus = "checking…";
            hudDirty = true;
            new Thread(() =>
            {
                ReleaseInfo r = null;
                string error = null;
                try { r = Updates.FetchLatest(); }
                catch (Exception ex) { error = ex.Message; }
                hud.BeginInvoke((MethodInvoker)(() =>
                {
                    checkingUpdate = false;
                    S.LastUpdateCheck = DateTime.UtcNow.Ticks;
                    if (error != null) { if (manual) updateStatus = error; }
                    else
                    {
                        latest = r;
                        updateStatus = r == null ? "no release published yet"
                            : r.IsNewer ? $"version {r.Version.ToString(3)} is available"
                            : "you have the latest version";
                        if (r != null && r.IsNewer && !updateNotified)
                        {
                            updateNotified = true;
                            tray.ShowBalloonTip(6000, $"Dirt Sweeper {r.Version.ToString(3)} is available",
                                "Open the settings (gear on the HUD), then About, to update in one click.", ToolTipIcon.Info);
                        }
                    }
                    Changed();
                }));
            }) { IsBackground = true, Name = "update check" }.Start();
        }

        void InstallUpdate()
        {
            if (latest == null || !latest.IsNewer || installingUpdate) return;
            if (!latest.CanInstall) { AppInfo.Open(latest.PageUrl); return; }
            installingUpdate = true;
            Sweeper.Stop("stopped to update");
            mgCts?.Cancel();
            updateStatus = $"downloading {latest.Version.ToString(3)}…";
            hudDirty = true;
            var release = latest;
            new Thread(() =>
            {
                string error = null;
                try { Updates.Install(release); }
                catch (Exception ex) { error = ex.Message; }
                hud.BeginInvoke((MethodInvoker)(() =>
                {
                    if (error != null)
                    {
                        installingUpdate = false;
                        updateStatus = error;
                        hudDirty = true;
                        return;
                    }
                    updateStatus = "restarting…";
                    S.Save();
                    Updates.Restart();
                    Exit();
                }));
            }) { IsBackground = true, Name = "update install" }.Start();
        }

        void StartPick(string what)
        {
            pickWhat = what;
            pickDeadline = Clock.Now + 3;
            hudDirty = true;
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
            if (g.Found) Native.SetForegroundWindow(g.Hwnd);
        }

        void ResetSettings()
        {
            if (Clock.Now > confirmResetUntil) { confirmResetUntil = Clock.Now + 3; hudDirty = true; return; }
            confirmResetUntil = 0;
            string path = S.GamePath;
            double fx = S.HudFx, fy = S.HudFy;
            S.SetDefaults();
            S.GamePath = path; S.HudFx = fx; S.HudFy = fy; S.Tab = 2;
            lastNote = "settings reset to defaults";
            Changed();
        }

        void ResetArea()
        {
            var d = new Settings();
            S.AreaLeft = d.AreaLeft; S.AreaTop = d.AreaTop; S.AreaRight = d.AreaRight; S.AreaBottom = d.AreaBottom;
            Changed();
        }

        /// <summary>Called whenever a setting changes.</summary>
        public void Changed()
        {
            saveAt = Clock.Now + 0.5;
            guidesDirty = true;
            hudDirty = true;
        }

        public void Exit()
        {
            tick.Stop();
            mgCts?.Cancel();
            Sweeper.Quit();
            hook.Set(false);
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
            hudDirty = true;
        }

        // ---------- circle size ----------
        /// <summary>Changes the circle's diameter by <paramref name="px"/> screen pixels, by adjusting px per level.</summary>
        void ResizeCircle(int dir, double px)
        {
            var lay = Game.Current.Layout;
            double d = S.Stat * S.PxPerStat + dir * px / lay.Scale;
            S.PxPerStat = Math.Max(0.05, d / Math.Max(1, S.Stat));
            Changed();
        }

        // ---------- mouse hook (only installed while calibrating, editing or the panel is open) ----------
        bool OnHookWheel(Point pt, int delta)
        {
            if (hudShown && hud.Contains(pt)) return hud.HandleWheel(pt, delta);
            if (!calibrating) return false;
            int notches = Math.Max(1, Math.Abs(delta) / 120) * Math.Sign(delta);
            ResizeCircle(notches, Shift ? 0.5 : Ctrl ? 10 : 2);
            UpdateRing(pt);
            return true; // don't let the game scroll while calibrating
        }

        void OnHookMove(Point pt) { if (calibrating) UpdateRing(pt); }

        // ---------- HUD position ----------
        void SaveHudPosition()
        {
            var w = Game.Current.Layout.Win;
            S.HudFx = (hud.BarPos.X - w.X) / (double)w.Width;
            S.HudFy = (hud.BarPos.Y - w.Y) / (double)w.Height;
            Changed();
        }

        Rectangle HudScreen() => Screen.FromRectangle(Game.Current.Layout.Win).Bounds;

        Point HudLocation()
        {
            var w = Game.Current.Layout.Win;
            int x = w.X + (int)Math.Round(S.HudFx * w.Width), y = w.Y + (int)Math.Round(S.HudFy * w.Height);
            var screen = HudScreen();
            x = Math.Max(screen.Left, Math.Min(x, screen.Right - hud.BarSize.Width));
            y = Math.Max(screen.Top, Math.Min(y, screen.Bottom - hud.BarSize.Height));
            return new Point(x, y);
        }

        // ---------- guides ----------
        int PreviewCount => SweepPath.Get(S, Game.Current.Layout).Count;

        GuideScene BuildScene()
        {
            var lay = Game.Current.Layout;
            var path = SweepPath.Get(S, lay);
            return new GuideScene
            {
                ShowStatic = S.ShowGuides,
                Editing = editingField,
                Area = SweepPath.Area(S, lay),
                Path = path,
                Radius = (float)(SweepPath.CircleDiameter(S, lay) / 2),
                TimerBox = lay.RightBox(S.TimerX, S.TimerY, S.TimerW, S.TimerH),
                Button = lay.Right(S.ButtonX, S.ButtonY),
                AreaLabel = $"{S.AreaLeft:0}, {S.AreaTop:0}  →  {S.AreaRight:0}, {S.AreaBottom:0}   ·   {path.Count} spots",
            };
        }

        void OnAreaDragged(RectangleF r)
        {
            var lay = Game.Current.Layout;
            double Snap(double v, double max) => Math.Round(v < 6 ? 0 : v > max - 6 ? max : v);
            S.AreaLeft = Math.Max(0, Snap((r.Left - lay.Win.X) / lay.Scale, 1920));
            S.AreaTop = Math.Max(0, Snap((r.Top - lay.Win.Y) / lay.Scale, 1080));
            S.AreaRight = Math.Min(1920, Snap((r.Right + 1 - lay.Win.X - lay.Extra) / lay.Scale, 1920));
            S.AreaBottom = Math.Min(1080, Snap((r.Bottom + 1 - lay.Win.Y) / lay.Scale, 1080));
            Changed();
        }

        // ---------- settings panel ----------
        void BuildPanel(PanelBuilder p)
        {
            var lay = Game.Current.Layout;
            p.Tabs(new[] { "Sweeper", "Timer game", "General", "About" }, S.Tab, i => { S.Tab = i; Changed(); });
            switch (S.Tab)
            {
                case 0: SweeperTab(p, lay); break;
                case 1: TimerTab(p); break;
                case 2: GeneralTab(p); break;
                default: AboutTab(p); break;
            }
        }

        static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));
        static int Clamp(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));

        void SweeperTab(PanelBuilder p, GameLayout lay)
        {
            double d = SweepPath.CircleDiameter(S, lay);
            p.Section("Hover circle");
            p.Stepper("stat", "Collision radius level", $"{S.Stat:0}",
                dir => { S.Stat = Clamp(Math.Round(S.Stat) + dir * (Shift ? 10 : 1), 1, 9999); Changed(); },
                "your collision radius upgrade level; bump it when you buy one", $"{d:0} px circle");
            p.Stepper("pps", "Px per level", $"{S.PxPerStat:0.00}",
                dir => { S.PxPerStat = Clamp(Math.Round(S.PxPerStat + dir * (Shift ? 0.1 : 0.01), 3), 0.05, 100); Changed(); },
                "circle diameter per level; easiest to set with Calibrate (Shift = ×10)");
            p.Buttons(B("cal", calibrating ? "Done calibrating  (F7)" : "Calibrate on screen  (F7)", ToggleCalibrate,
                "shows a green ring at the cursor; scroll over the game to resize it", active: calibrating));
            if (calibrating)
                p.Text("Move over the dig field and scroll until the green ring matches the game's hover circle. " +
                       "Shift + scroll = fine, Ctrl + scroll = coarse. Press F7 when it matches.", Theme.Green);

            p.Section("Speed");
            double hz = Game.Current.RefreshHz;
            p.Stepper("hold", "Hold on each spot", $"{S.HoldMs} ms",
                dir => { S.HoldMs = Clamp(S.HoldMs + dir * (Shift ? 10 : 1), 1, 500); Changed(); },
                "time the cursor rests on each spot; raise it if tools get missed", $"≈ {S.HoldMs * hz / 1000:0.#} frames");
            p.Stepper("spacing", "Spot spacing", $"{S.Spacing} %",
                dir => { S.Spacing = Clamp(S.Spacing + dir * (Shift ? 1 : 5), 30, 100); Changed(); },
                "share of the circle the spots rely on; lower = more overlap, safer but slower (Shift = 1%)");
            int spots = PreviewCount;
            p.Info("Full sweep", $"{spots} spots  ·  {spots * Sweeper.HoldSeconds:0.00} s per pass", Theme.Green);

            p.Section("Dig field");
            p.Info("Area  (1920×1080 layout)", $"{S.AreaLeft:0}, {S.AreaTop:0}  →  {S.AreaRight:0}, {S.AreaBottom:0}");
            p.Buttons(
                B("edit", editingField ? "Done editing" : "Edit on screen", ToggleEditField,
                    "drag the yellow edges and corners over the game", active: editingField, activeColor: Theme.Yellow),
                B("areaReset", "Reset field", ResetArea, "back to the default field (0, 0 → 1485, 1080)"));

            p.Section("Behaviour");
            p.Toggle("pause", "Stop when I move the mouse", S.PauseOnMouse, () => { S.PauseOnMouse = !S.PauseOnMouse; Changed(); },
                "moving the mouse yourself stops the sweep");
            p.Toggle("focus", "Only sweep while the game is in front", S.OnlyWhenFocused, () => { S.OnlyWhenFocused = !S.OnlyWhenFocused; Changed(); },
                "pauses while you're in another app");
            p.Toggle("shift", "Vary the pattern every pass", S.Shift, () => { S.Shift = !S.Shift; Changed(); },
                "mirrors the spots each pass so they don't always land on the same pixels");
            p.Toggle("pingpong", "Alternate direction every pass", S.PingPong, () => { S.PingPong = !S.PingPong; Changed(); },
                "sweeps back the other way each pass");
            p.Space(8);
            p.Buttons(
                B("sweep", Sweeper.Running ? "Stop  (F5)" : "Start sweeping  (F5)", ToggleSweep, "start / stop sweeping", primary: true),
                B("guides", "Guides  (F6)", ToggleGuides, "show the dig field, spots and timer box", active: S.ShowGuides, activeColor: Theme.Blue));
        }

        void TimerTab(PanelBuilder p)
        {
            p.Section("Red-button timer game");
            p.Buttons(B("arm", MinigameActive ? "Cancel  (F8)" : "Arm timer game  (F8)", ToggleTimerGame,
                "arm it, then click Play in the game; every level is played by itself", primary: !MinigameActive,
                active: MinigameActive, activeColor: Theme.Accent));
            var levels = new HashSet<int>((TimerGame.ParseLevels(S.Levels) ?? new List<double>()).Select(v => (int)Math.Round(v)));
            p.Chips("lvl", "Levels (seconds)", 6, levels.Contains, n =>
            {
                if (levels.Contains(n)) { if (levels.Count > 1) levels.Remove(n); }
                else levels.Add(n);
                S.Levels = string.Join(",", levels.OrderBy(v => v));
                Changed();
            }, "which countdowns to play; turn on 4, 5… when you unlock them");
            p.Stepper("offset", "Click offset", $"{S.OffsetMs:+0;-0;0} ms",
                dir => { S.OffsetMs = Clamp(S.OffsetMs + dir * (Shift ? 10 : 1), -500, 500); Changed(); },
                "game says late by X ms → lower it by X; early → raise it");
            p.Toggle("resume", "Resume sweeping afterwards", S.ResumeAfter, () => { S.ResumeAfter = !S.ResumeAfter; Changed(); },
                "if it was sweeping before the timer game, carry on afterwards");

            p.Section("Positions  (1920×1080 layout)");
            p.Info("Timer digits centre", $"{S.TimerX:0}, {S.TimerY:0}");
            p.Info("Red button", $"{S.ButtonX:0}, {S.ButtonY:0}");
            string countdown = $"{Math.Max(0, Math.Ceiling(pickDeadline - Clock.Now))}";
            p.Buttons(
                B("pickT", pickWhat == "timer" ? $"Hover digits… {countdown}" : "Pick timer", () => StartPick("timer"),
                    "click, then hover the timer digits within 3 s", active: pickWhat == "timer", activeColor: Theme.Blue),
                B("pickB", pickWhat == "button" ? $"Hover button… {countdown}" : "Pick button", () => StartPick("button"),
                    "click, then hover the red button within 3 s", active: pickWhat == "button", activeColor: Theme.Blue));
            p.Stepper("tw", "Timer box width", $"{S.TimerW:0}", dir => { S.TimerW = Clamp(S.TimerW + dir * (Shift ? 10 : 2), 10, 600); Changed(); },
                "size of the red box the digits are read from (guides show it)");
            p.Stepper("th", "Timer box height", $"{S.TimerH:0}", dir => { S.TimerH = Clamp(S.TimerH + dir * (Shift ? 10 : 2), 10, 300); Changed(); },
                "size of the red box the digits are read from (guides show it)");

            p.Section("Detection  (advanced)");
            p.Stepper("minrun", "Min countdown run", $"{S.MinRunMs} ms", dir => { S.MinRunMs = Clamp(S.MinRunMs + dir * 25, 50, 700); Changed(); },
                "raise it if it clicks too early");
            p.Stepper("bright", "Text brightness", $"{S.TextMin}", dir => { S.TextMin = Clamp(S.TextMin + dir * 5, 0, 254); Changed(); },
                "lower it if it never sees the countdown");

            p.Section("Log");
            p.Log(log, 4);
        }

        void GeneralTab(PanelBuilder p)
        {
            var g = Game.Current;
            p.Section("Game");
            p.Info("Dirt Clicker", g.Found ? $"running  ·  {g.Layout.Win.Width}×{g.Layout.Win.Height}  ·  {g.RefreshHz} Hz" : "not running",
                g.Found ? Theme.Green : Theme.Yellow);
            if (!g.Found) p.Buttons(B("launch", "Launch game", LaunchGame, "starts Dirt Clicker", primary: true));

            p.Section("Overlay");
            p.Toggle("guidesT", "Show guides  (F6)", S.ShowGuides, ToggleGuides, "draws the dig field, sweep spots, timer box and button over the game");
            p.Toggle("hudT", "Show the HUD bar  (F9)", S.ShowHud, ToggleHud, "hides this bar; F9 or the tray icon brings it back");

            p.Section("Hotkeys");
            p.Info("F5", "start / stop sweeping");
            p.Info("F6", "show / hide guides");
            p.Info("F7", "calibration ring (scroll to resize)");
            p.Info("F8", "arm / cancel the timer game");
            p.Info("F9", "show / hide the HUD");

            p.Section("App");
            bool confirm = Clock.Now < confirmResetUntil;
            p.Buttons(
                B("reset", confirm ? "Click again to reset" : "Reset all settings", ResetSettings,
                    "puts every setting back to its default (keeps the HUD position)", active: confirm, activeColor: Theme.Red),
                B("exit", "Exit Dirt Sweeper", Exit, "closes the app"));
            p.Buttons(B("folder", "Open settings folder", OpenSettingsFolder,
                "shows settings.json in Explorer; delete that folder to remove every trace of Dirt Sweeper"));
            p.Text("Settings file: " + Settings.FilePath);
        }

        void OpenSettingsFolder()
        {
            if (!File.Exists(Settings.FilePath)) S.Save();
            try { Process.Start("explorer.exe", "/select,\"" + Settings.FilePath + "\""); }
            catch (Exception) { AppInfo.Open(Path.GetDirectoryName(Settings.FilePath)); }
        }

        void AboutTab(PanelBuilder p)
        {
            p.Section("Dirt Sweeper");
            p.Info("Version", AppInfo.VersionText);
            p.Text("Sweeps the Dirt Clicker dig field and plays the timer minigame for you. Unofficial fan-made tool, " +
                   "not affiliated with the developers of Dirt Clicker. Free and open source (GPL-3.0).");

            p.Section("Updates");
            bool newer = latest != null && latest.IsNewer;
            string last = S.LastUpdateCheck > 0 ? new DateTime(S.LastUpdateCheck, DateTimeKind.Utc).ToLocalTime().ToString("g") : "never";
            p.Info("Status", updateStatus != "" ? updateStatus : "last checked: " + last, newer ? Theme.Blue : (Color?)null);
            if (newer)
                p.Buttons(
                    B("install", installingUpdate ? "Updating…" : $"Update to {latest.Version.ToString(3)}", InstallUpdate,
                        latest.CanInstall ? "downloads the new exe, checks it and restarts Dirt Sweeper" : "opens the download page",
                        primary: !installingUpdate, active: installingUpdate, activeColor: Theme.Blue),
                    B("notes", "What's new", () => AppInfo.Open(latest.PageUrl), "opens the release notes on GitHub"));
            else
                p.Buttons(B("check", checkingUpdate ? "Checking…" : "Check for updates", () => CheckForUpdates(true),
                    "asks GitHub for the latest release", active: checkingUpdate, activeColor: Theme.Blue));
            p.Toggle("autoupd", "Check for updates automatically", S.CheckUpdates, () => { S.CheckUpdates = !S.CheckUpdates; Changed(); },
                "at startup and once a day; the only time Dirt Sweeper goes online");

            p.Section("Feedback");
            p.Text("Found a bug, or have an idea for a feature? Open an issue on GitHub. Every report helps make Dirt Sweeper better.",
                Theme.Text);
            p.Buttons(B("issue", "Report a bug or request a feature", () => AppInfo.Open(AppInfo.IssuesUrl),
                "opens GitHub issues in your browser", icon: Icons.GitHub));

            p.Section("Links");
            p.Buttons(
                B("github", "GitHub", () => AppInfo.Open(AppInfo.RepoUrl), "source code, releases and the changelog", icon: Icons.GitHub),
                B("coffee", "Buy me a coffee", () => AppInfo.Open(AppInfo.BuyMeACoffeeUrl), "support development, thank you!",
                    icon: (g, r, c) => Icons.Coffee(g, r, c)));
            p.Buttons(
                B("website", "Website", () => AppInfo.Open(AppInfo.WebsiteUrl), "eyyminda.github.io/Dirt-Sweeper",
                    icon: (g, r, c) => Theme.Draw(g, Glyph.Globe, Theme.Icons, c, RectangleF.Inflate(r, 3, 3), StringAlignment.Center)),
                B("author", "eyyMinda", () => AppInfo.Open(AppInfo.AuthorUrl), "the author on GitHub", icon: Icons.GitHub));
        }

        // ---------- main loop (UI thread) ----------
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
            if (now >= nextUpdateCheck)
            {
                nextUpdateCheck = now + 24 * 3600;
                if (S.CheckUpdates) CheckForUpdates(false);
            }

            while (timerGame.LogLines.TryDequeue(out var line))
            {
                log.Add(line);
                if (log.Count > 100) log.RemoveAt(0);
                hudDirty = true;
            }

            // the HUD and guides only show over the game (or everywhere while it isn't running)
            var fg = Native.GetForegroundWindow();
            bool gameContext = !g.Found || fg == g.Hwnd || fg == hud.Handle;

            UpdateHud(g, gameContext, now);
            UpdateGuides(g, gameContext);
            UpdateRing(null, gameContext);

            hook.Set(gameContext && !Busy && (calibrating || editingField || (hudShown && hud.PanelOpen)));
            tick.Interval = calibrating || editingField || (Sweeper.Running && S.ShowGuides) ? 15 : 50;

            if (now >= saveAt)
            {
                saveAt = double.MaxValue;
                if (!S.Save()) { lastNote = "couldn't save settings to " + Settings.FilePath; hudDirty = true; }
            }
        }

        void UpdateHud(GameState g, bool gameContext, double now)
        {
            bool busy = Busy;
            btnLaunch.Visible = !g.Found;
            btnSweep.Glyph = Sweeper.Running ? Glyph.Stop : Glyph.Play;
            btnSweep.Active = Sweeper.Running;
            btnTimer.Active = MinigameActive;
            btnGuides.Active = S.ShowGuides;
            btnPanel.Active = hud.PanelOpen;
            bool updateReady = latest != null && latest.IsNewer;
            btnPanel.Badge = updateReady;

            if (pickWhat != null)
            {
                hud.Dot = Theme.Blue;
                hud.Title = $"Hover the {(pickWhat == "timer" ? "timer digits" : "red button")}…";
                hud.Detail = $"capturing the position in {Math.Ceiling(pickDeadline - now)}";
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
                hud.Title = $"Calibrating · {SweepPath.CircleDiameter(S, g.Layout):0} px · F7 to finish";
                hud.Detail = "scroll over the game until the green ring matches its hover circle";
            }
            else if (editingField)
            {
                hud.Dot = Theme.Yellow;
                hud.Title = "Editing the dig field";
                hud.Detail = "drag the yellow edges or corners, then click Done editing";
            }
            else
            {
                hud.Dot = g.Found ? Theme.Sub : Theme.Yellow;
                hud.Title = g.Found ? "Ready" : "Game not running";
                string note = Sweeper.Note != "" && lastNote == "" ? Sweeper.Note : lastNote;
                if (note == "" && updateReady)
                {
                    hud.Dot = Theme.Blue;
                    note = installingUpdate ? updateStatus : $"update {latest.Version.ToString(3)} available · gear → About";
                }
                hud.Detail = note != "" ? note : g.Found ? "F5 sweep · F8 timer game · F6 guides · F7 calibrate" : "start Dirt Clicker, or click the controller icon";
            }

            bool show = S.ShowHud && gameContext;
            if (show)
            {
                hud.Busy = busy;
                hud.SetClickThrough(busy);
                if (!hud.Dragging)
                {
                    var loc = HudLocation();
                    // the bar's text changes often while busy, so redraw a few times a second; otherwise only on change
                    if (!hudShown || hudDirty || loc != hud.BarPos || now - lastHudDraw > (busy || pickWhat != null ? 0.1 : 0.5))
                    {
                        hud.Place(loc, HudScreen());
                        lastHudDraw = now;
                        hudDirty = false;
                    }
                }
                if (!hudShown) { hud.Show(); hudShown = true; }
            }
            else if (hudShown) { hud.Hide(); hudShown = false; }
        }

        void UpdateGuides(GameState g, bool gameContext)
        {
            bool show = (S.ShowGuides || editingField || pickWhat != null) && gameContext;
            if (show)
            {
                if (guides.Bounds != g.Layout.Win) { guides.Bounds = g.Layout.Win; guidesDirty = true; }
                if (!guidesShown)
                {
                    guides.Show();
                    guidesShown = true;
                    guidesDirty = true;
                    // the HUD sits inside the field; keep it above the guides so it stays clickable in edit mode
                    if (hudShown) Native.RaiseTopmost(hud.Handle);
                }
                if (guidesDirty) { guides.Redraw(); guidesDirty = false; }
            }
            else if (guidesShown) { guides.Hide(); guidesShown = false; }
        }

        /// <summary>Calibration ring at the cursor, or the sweeper's current spot while guides are on.</summary>
        void UpdateRing(Point? cursor, bool gameContext = true)
        {
            var lay = Game.Current.Layout;
            float r = (float)(SweepPath.CircleDiameter(S, lay) / 2);
            if (!gameContext) ring.Set(null, 0, null, Theme.Green);
            else if (calibrating)
            {
                var c = cursor ?? Native.CursorPos();
                if (hudShown && hud.Contains(c)) ring.Set(null, 0, null, Theme.Green);
                else ring.Set(c, r, $"{2 * r:0} px  ·  scroll to resize  ·  F7 when it matches", Theme.Green);
            }
            else if (Sweeper.Running && Sweeper.HasPos && S.ShowGuides)
                ring.Set(new Point(Sweeper.PosX, Sweeper.PosY), r, null, Theme.Green);
            else ring.Set(null, 0, null, Theme.Green);
        }
    }
}
