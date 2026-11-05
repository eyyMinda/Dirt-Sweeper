using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;

namespace DirtSweeper
{
    /// <summary>A minigame the app can play by itself. Add new ones by deriving from this.</summary>
    abstract class Minigame
    {
        public abstract string Name { get; }
        public volatile string Status = "";
        public volatile string Result = "";
        public readonly ConcurrentQueue<string> LogLines = new ConcurrentQueue<string>();

        protected void Log(string text) { LogLines.Enqueue(DateTime.Now.ToString("HH:mm:ss  ") + text); }

        /// <summary>Plays one round. Returns true if it finished normally.</summary>
        public abstract bool Run(Settings s, GameState game, CancellationToken ct);
    }

    /// <summary>Copies one small screen rectangle and tracks how many "text" pixels changed since the last copy.</summary>
    sealed class ScreenGrab : IDisposable
    {
        readonly IntPtr screenDc, memDc, bmp, oldBmp, bits;
        readonly Rectangle box;
        readonly byte[] buf;
        byte[] prevMask, curMask;
        readonly byte[] refMask;
        bool hasPrev;

        public double CapturedAt;
        /// <summary>Pixels that differ from the reference picture (see <see cref="SetReference"/>).</summary>
        public int RefDiff;
        /// <summary>Text pixels in the reference picture.</summary>
        public int RefCount;

        public ScreenGrab(Rectangle r)
        {
            box = r;
            screenDc = Native.GetDC(IntPtr.Zero);
            memDc = Native.CreateCompatibleDC(screenDc);
            var bmi = new Native.BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER)), biWidth = r.Width, biHeight = -r.Height,
                biPlanes = 1, biBitCount = 32,
            };
            bmp = Native.CreateDIBSection(memDc, ref bmi, 0, out bits, IntPtr.Zero, 0);
            oldBmp = Native.SelectObject(memDc, bmp);
            buf = new byte[r.Width * r.Height * 4];
            prevMask = new byte[r.Width * r.Height];
            curMask = new byte[r.Width * r.Height];
            refMask = new byte[r.Width * r.Height];
        }

        /// <summary>Remembers the last capture as the "still" picture that later captures are compared to.</summary>
        public void SetReference()
        {
            Buffer.BlockCopy(prevMask, 0, refMask, 0, refMask.Length);
            int n = 0;
            foreach (var m in refMask) n += m;
            RefCount = n;
        }

        /// <summary>
        /// Captures the box and returns how many pixels flipped between text and not-text.
        /// Text = all of R, G, B above the threshold; dirt particles are orange/yellow
        /// (blue near zero) so they never count as text.
        /// </summary>
        public int CaptureDiff(int threshold)
        {
            Native.BitBlt(memDc, 0, 0, box.Width, box.Height, screenDc, box.X, box.Y, 0x00CC0020);
            Native.GdiFlush();
            CapturedAt = Clock.Now;
            Marshal.Copy(bits, buf, 0, buf.Length);
            int n = 0, rd = 0;
            for (int i = 0, p = 0; i < curMask.Length; i++, p += 4)
            {
                byte m = (byte)(buf[p] > threshold && buf[p + 1] > threshold && buf[p + 2] > threshold ? 1 : 0);
                curMask[i] = m;
                if (m != prevMask[i]) n++;
                if (m != refMask[i]) rd++;
            }
            RefDiff = rd;
            var t = prevMask; prevMask = curMask; curMask = t;
            if (!hasPrev) { hasPrev = true; return 0; }
            return n;
        }

        public void Dispose()
        {
            Native.SelectObject(memDc, oldBmp);
            Native.DeleteObject(bmp);
            Native.DeleteDC(memDc);
            Native.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    /// <summary>
    /// The red-button timer: each level shows "0N.00", beeps, counts down visibly for ~0.75s,
    /// then hides. The countdown keeps the timer box changing every frame for that long, and
    /// (unlike a dirt particle drifting over the still digits) soon makes it look very different
    /// from the still "0N.00". The first frame of such a run is the start; the button gets
    /// clicked N seconds later.
    /// </summary>
    sealed class TimerGame : Minigame
    {
        public override string Name => "Timer game";

        public static List<double> ParseLevels(string text)
        {
            var levels = new List<double>();
            foreach (var part in (text ?? "").Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || v <= 0 || v > 60)
                    return null;
                levels.Add(v);
            }
            return levels.Count > 0 ? levels : null;
        }

        public override bool Run(Settings s, GameState game, CancellationToken ct)
        {
            var levels = ParseLevels(s.Levels);
            if (levels == null) { Result = "the levels list is not valid"; return false; }
            var lay = game.Layout;
            var box = lay.RightBox(s.TimerX, s.TimerY, s.TimerW, s.TimerH);
            var button = lay.Right(s.ButtonX, s.ButtonY);
            Native.timeBeginPeriod(1);
            try
            {
                using (var grab = new ScreenGrab(box))
                {
                    if (ct.WaitHandle.WaitOne(150)) return false; // let the hotkey press settle
                    double deadline = Clock.Now + 60; // time allowed to click Play
                    for (int i = 0; i < levels.Count; i++)
                    {
                        double secs = levels[i];
                        string lvl = $"level {i + 1}/{levels.Count}";
                        Status = i == 0 ? "click Play in the game" : $"{lvl}: waiting for the {secs:0.##}s countdown";
                        double? t0 = WaitForCountdown(grab, s, ct, deadline);
                        if (t0 == null)
                        {
                            if (!ct.IsCancellationRequested)
                            {
                                Result = "no countdown seen - check the timer box with the guides (F6)";
                                Log($"L{i + 1}: no countdown seen, gave up");
                            }
                            return false;
                        }
                        double target = t0.Value + secs + s.OffsetMs / 1000.0;
                        Status = $"{lvl}: clicking at {secs:0.##}s";
                        Native.MoveTo(button.X, button.Y);
                        Clock.SleepUntil(target, ct);
                        if (ct.IsCancellationRequested) return false;
                        Native.MouseDown();
                        double late = (Clock.Now - target) * 1000;
                        Thread.Sleep(30);
                        Native.MouseUp();
                        Log($"L{i + 1} {secs:0.##}s: clicked ({late:0.0} ms scheduling delay)");
                        if (ct.WaitHandle.WaitOne(1000)) return false; // skip the result screen
                        deadline = Clock.Now + 30;
                    }
                }
            }
            finally { Native.timeEndPeriod(1); }
            Result = "timer game done";
            return true;
        }

        internal static double? WaitForCountdown(ScreenGrab grab, Settings s, CancellationToken ct, double deadline)
        {
            const int minPixels = 25;         // smaller changes are noise
            const double maxGap = 0.05;       // a pause longer than this ends a run
            const double minRefChange = 0.18; // share of the still digits a countdown changes (a particle covers ~10%)
            const int minStillText = 150;     // the countdown starts from the visible "0N.00", never from an empty box
            double minRun = s.MinRunMs / 1000.0;
            double? runStart = null, bigStart = null;
            double last = 0;
            grab.CaptureDiff(s.TextMin);
            grab.SetReference();
            while (!ct.IsCancellationRequested && Clock.Now < deadline)
            {
                Thread.Sleep(1);
                int changed = grab.CaptureDiff(s.TextMin);
                double now = grab.CapturedAt;
                bool inRun = runStart != null && now - last <= maxGap;
                if (changed < minPixels)
                {
                    if (!inRun) { runStart = bigStart = null; grab.SetReference(); }
                    continue;
                }
                if (!inRun) { runStart = now; bigStart = null; }
                last = now;
                // the countdown's first frame already looks very different from the still digits;
                // a particle drifting over them never does, so it can't pull the start time earlier
                if (bigStart == null && grab.RefCount >= minStillText &&
                    grab.RefDiff >= Math.Max(4 * minPixels, minRefChange * grab.RefCount))
                    bigStart = now;
                if (bigStart != null && now - bigStart.Value >= minRun) return bigStart;
            }
            return null;
        }
    }
}
