using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace DirtSweeper
{
    static class SweepPath
    {
        public static double CircleDiameter(Settings s, GameLayout lay) => lay.Px(s.Stat * s.PxPerStat);

        /// <summary>Play area in screen pixels (inclusive edges), kept on the game's monitor.</summary>
        public static RectangleF Area(Settings s, GameLayout lay)
        {
            double l = lay.X(s.AreaLeft), t = lay.Y(s.AreaTop);
            double r = lay.XRight(s.AreaRight) - 1, b = lay.Y(s.AreaBottom) - 1;
            // Godot's borderless fullscreen makes the window a couple of pixels bigger than the
            // screen; the cursor can't reach those pixels, so keep the area on the monitor
            var mon = Screen.FromRectangle(lay.Win).Bounds;
            l = Math.Max(l, mon.Left); t = Math.Max(t, mon.Top);
            r = Math.Min(r, mon.Right - 1); b = Math.Min(b, mon.Bottom - 1);
            r = Math.Max(l, r); b = Math.Max(t, b);
            return RectangleF.FromLTRB((float)l, (float)t, (float)r, (float)b);
        }

        static readonly object cacheLock = new object();
        static string cacheKey;
        static List<Point> cached;

        /// <summary>
        /// The sweep path for the current settings: the fewest spots that provably cover the whole
        /// area, in snake order. Cached, so calling it every pass costs nothing.
        /// </summary>
        public static List<Point> Get(Settings s, GameLayout lay)
        {
            var a = Area(s, lay);
            double r = Math.Max(2, CircleDiameter(s, lay) / 2 * s.Spacing / 100);
            string key = $"{a}|{r:0.###}";
            lock (cacheLock)
            {
                if (key != cacheKey)
                {
                    cached = Fit(a.Left, a.Top, a.Right, a.Bottom, r);
                    cacheKey = key;
                }
                return new List<Point>(cached);
            }
        }

        /// <summary>The same spots mirrored, so successive passes don't always land on the same pixels.</summary>
        public static void Mirror(List<Point> path, RectangleF a, bool x, bool y)
        {
            for (int i = 0; i < path.Count; i++)
            {
                var p = path[i];
                path[i] = new Point(x ? (int)Math.Round(a.Left + a.Right - p.X) : p.X,
                                    y ? (int)Math.Round(a.Top + a.Bottom - p.Y) : p.Y);
            }
        }

        // ---------- fitting ----------
        // Two families of candidate layouts are tried, and every candidate is checked for full
        // coverage before it can win; the one with the fewest spots is used:
        //  * hex lattices at several offsets, with spots outside the area pulled onto its edge
        //    (the classic densest covering, good when the area is many circles wide);
        //  * staggered grids stretched to fit the area exactly: rows alternate between k spots at
        //    cell centres and k+1 spots at cell edges (good when only a few circles fit).

        internal static List<Point> Fit(double L, double T, double R, double B, double r)
        {
            var check = new CoverageCheck(L, T, R, B, r);
            List<PointF> best = null;
            int bestCount = int.MaxValue;
            bool Try(List<PointF> c)
            {
                if (c.Count >= bestCount || !check.Covers(c)) return false;
                best = c;
                bestCount = c.Count;
                return true;
            }

            // hex lattices spaced for the checker's slightly smaller radius, so they pass when exact
            double rh = check.SafeRadius;
            foreach (bool vertical in new[] { false, true })
                for (int i = 0; i < 8; i++)
                    for (int j = 0; j < 8; j++)
                        Try(Hex(L, T, R, B, rh, i / 8.0, j / 8.0, vertical));

            foreach (bool vertical in new[] { false, true })
            {
                double u0 = vertical ? T : L, u1 = vertical ? B : R, v0 = vertical ? L : T, v1 = vertical ? R : B;
                int nMax = (int)Math.Ceiling((v1 - v0) / r) + 3, kMax = (int)Math.Ceiling((u1 - u0) / r) + 3;
                foreach (bool edgeRows in new[] { false, true })
                    for (int parity = 0; parity < 2; parity++)
                        for (int n = 1; n <= nMax; n++)
                            for (int k = 1; k <= kMax; k++)
                            {
                                if (StaggeredCount(n, k, parity) >= bestCount) break; // more k only adds spots
                                if (Try(Staggered(n, k, parity, edgeRows, u0, u1, v0, v1, vertical))) break;
                            }
            }

            if (best == null) // can't happen (dense grids always cover), but never sweep nothing
                best = Staggered((int)Math.Ceiling((B - T) / r) + 3, (int)Math.Ceiling((R - L) / r) + 3, 0, true, L, R, T, B, false);
            return Order(best);
        }

        static int StaggeredCount(int n, int k, int parity)
        {
            int edgeRows = 0;
            for (int j = 0; j < n; j++) if (((j + parity) & 1) == 1) edgeRows++;
            return n * k + edgeRows;
        }

        static List<PointF> Staggered(int n, int k, int parity, bool edgeRows, double u0, double u1, double v0, double v1, bool vertical)
        {
            var pts = new List<PointF>();
            for (int j = 0; j < n; j++)
            {
                bool edges = ((j + parity) & 1) == 1;
                int m = edges ? k + 1 : k;
                double v = n == 1 ? (v0 + v1) / 2 : edgeRows ? v0 + (v1 - v0) * j / (n - 1) : v0 + (v1 - v0) * (j + 0.5) / n;
                for (int i = 0; i < m; i++)
                {
                    double u = edges ? u0 + (u1 - u0) * i / k : u0 + (u1 - u0) * (i + 0.5) / k;
                    pts.Add(vertical ? new PointF((float)v, (float)u) : new PointF((float)u, (float)v));
                }
            }
            return pts;
        }

        /// <summary>Hex lattice spots whose circle reaches the area, pulled onto the area where outside.</summary>
        static List<PointF> Hex(double L, double T, double R, double B, double r, double phx, double phy, bool vertical)
        {
            if (vertical) { (L, T) = (T, L); (R, B) = (B, R); }
            double dx = Math.Sqrt(3) * r, dy = 1.5 * r, ox = L + phx * dx, oy = T + phy * dy;
            var pts = new List<PointF>();
            int j0 = (int)Math.Floor((T - r - oy) / dy), j1 = (int)Math.Ceiling((B + r - oy) / dy);
            for (int j = j0; j <= j1; j++)
            {
                double y = oy + j * dy, off = ox + ((j & 1) != 0 ? dx / 2 : 0);
                int i0 = (int)Math.Floor((L - r - off) / dx), i1 = (int)Math.Ceiling((R + r - off) / dx);
                PointF? last = null;
                for (int i = i0; i <= i1; i++)
                {
                    double x = off + i * dx;
                    double nx = Math.Min(Math.Max(x, L), R), ny = Math.Min(Math.Max(y, T), B);
                    if ((x - nx) * (x - nx) + (y - ny) * (y - ny) > r * r) continue;
                    var p = vertical ? new PointF((float)ny, (float)nx) : new PointF((float)nx, (float)ny);
                    if (last != null && Math.Abs(last.Value.X - p.X) + Math.Abs(last.Value.Y - p.Y) < 2) continue;
                    pts.Add(p);
                    last = p;
                }
            }
            return pts;
        }

        /// <summary>Snake order: group spots into rows (or columns) and reverse every other one.</summary>
        static List<Point> Order(List<PointF> spots)
        {
            var pts = new List<Point>();
            foreach (var p in spots) pts.Add(new Point((int)Math.Round(p.X), (int)Math.Round(p.Y)));
            // candidates are generated row by row along their main axis; detect it from the first two spots
            bool vertical = pts.Count > 1 && Math.Abs(pts[1].X - pts[0].X) < Math.Abs(pts[1].Y - pts[0].Y);
            var rows = new List<List<Point>>();
            foreach (var p in pts)
            {
                int key = vertical ? p.X : p.Y;
                if (rows.Count == 0 || Math.Abs((vertical ? rows[rows.Count - 1][0].X : rows[rows.Count - 1][0].Y) - key) > 1)
                    rows.Add(new List<Point>());
                rows[rows.Count - 1].Add(p);
            }
            var outp = new List<Point>();
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].Sort((a, b) => vertical ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
                if ((i & 1) == 1) rows[i].Reverse();
                foreach (var p in rows[i])
                    if (outp.Count == 0 || outp[outp.Count - 1] != p) outp.Add(p);
            }
            return outp;
        }

        /// <summary>
        /// Checks coverage on a sample grid of spacing s, demanding each sample be within
        /// r - s/√2 - 1 of a spot. Every point of the area is within s/√2 of a sample, so passing
        /// guarantees every point is within r, even after spots are rounded to whole pixels.
        /// </summary>
        sealed class CoverageCheck
        {
            readonly double l, t, cell, rr;
            readonly double[] xs, ys;
            readonly int cols, rows;
            public readonly double SafeRadius;

            public CoverageCheck(double L, double T, double R, double B, double r)
            {
                double s = Math.Max(1.5, r / 20);
                SafeRadius = Math.Max(1, r - s * 0.7072 - 1);
                rr = SafeRadius * SafeRadius;
                l = L; t = T; cell = SafeRadius;
                xs = Samples(L, R - L, s);
                ys = Samples(T, B - T, s);
                cols = (int)((R - L) / cell) + 3;
                rows = (int)((B - T) / cell) + 3;
            }

            static double[] Samples(double a, double len, double s)
            {
                int n = Math.Max(1, (int)Math.Ceiling(len / s));
                var r = new double[n + 1];
                for (int i = 0; i <= n; i++) r[i] = a + len * i / n;
                return r;
            }

            int Cx(double x) => Math.Max(0, Math.Min(cols - 1, (int)((x - l) / cell) + 1));
            int Cy(double y) => Math.Max(0, Math.Min(rows - 1, (int)((y - t) / cell) + 1));

            public bool Covers(List<PointF> spots)
            {
                // bucket the spots by cell so each sample only looks at its neighbourhood
                var buckets = new List<PointF>[cols * rows];
                foreach (var p in spots)
                {
                    int i = Cy(p.Y) * cols + Cx(p.X);
                    (buckets[i] ?? (buckets[i] = new List<PointF>())).Add(p);
                }
                foreach (double y in ys)
                {
                    int cy = Cy(y);
                    foreach (double x in xs)
                        if (!Near(buckets, Cx(x), cy, x, y)) return false;
                }
                return true;
            }

            bool Near(List<PointF>[] buckets, int cx, int cy, double x, double y)
            {
                for (int by = Math.Max(0, cy - 1); by <= Math.Min(rows - 1, cy + 1); by++)
                    for (int bx = Math.Max(0, cx - 1); bx <= Math.Min(cols - 1, cx + 1); bx++)
                    {
                        var b = buckets[by * cols + bx];
                        if (b == null) continue;
                        foreach (var p in b)
                        {
                            double dx = p.X - x, dy = p.Y - y;
                            if (dx * dx + dy * dy <= rr) return true;
                        }
                    }
                return false;
            }
        }
    }

    /// <summary>Moves the cursor spot to spot, holding each for a set time.</summary>
    sealed class Sweeper
    {
        readonly Settings s;
        readonly GameTracker game;
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        volatile bool running, quit;

        public volatile string Note = "";
        public volatile int Passes;
        public volatile bool HasPos;
        public volatile int PosX, PosY;

        public Sweeper(Settings settings, GameTracker tracker)
        {
            s = settings;
            game = tracker;
            new Thread(Loop) { IsBackground = true, Name = "sweeper" }.Start();
        }

        public bool Running => running;
        public double HoldSeconds => Math.Max(1, s.HoldMs) / 1000.0;

        public void Start() { Note = ""; running = true; wake.Set(); }
        public void Stop(string reason = null) { running = false; if (reason != null) Note = reason; }
        public void Quit() { quit = true; running = false; wake.Set(); }

        void Loop()
        {
            var rnd = new Random();
            int passNo = 0;
            while (!quit)
            {
                if (!running) { HasPos = false; wake.WaitOne(); continue; }
                Native.timeBeginPeriod(1);
                try
                {
                    while (running && !quit)
                        if (RunPass(rnd, passNo)) passNo++;
                }
                finally { Native.timeEndPeriod(1); }
            }
        }

        // returns true when the pass was swept, false when it waited or was interrupted
        bool RunPass(Random rnd, int passNo)
        {
            var g = game.Current;
            if (s.OnlyWhenFocused && !g.InFront)
            {
                Note = "waiting for the game to be in front";
                HasPos = false;
                Thread.Sleep(100);
                return false;
            }
            var path = SweepPath.Get(s, g.Layout);
            if (s.Shift) SweepPath.Mirror(path, SweepPath.Area(s, g.Layout), rnd.Next(2) == 1, rnd.Next(2) == 1);
            if (s.PingPong && passNo % 2 == 1) path.Reverse();
            double hold = HoldSeconds, next = Clock.Now;
            foreach (var p in path)
            {
                if (!running || quit) return false;
                if (s.OnlyWhenFocused && !g.InFront) return false;
                if (s.PauseOnMouse && HasPos)
                {
                    var c = Native.CursorPos();
                    if (Math.Abs(c.X - PosX) + Math.Abs(c.Y - PosY) > 40)
                    {
                        Stop("stopped because you moved the mouse");
                        return false;
                    }
                }
                Native.MoveTo(p.X, p.Y);
                PosX = p.X; PosY = p.Y; HasPos = true;
                next += hold;
                Clock.SleepUntil(next);
                if (Clock.Now - next > 0.1) next = Clock.Now; // fell behind (system hiccup): resync
            }
            Passes++;
            Note = $"{path.Count} spots · {path.Count * hold:0.00} s per sweep";
            return true;
        }
    }
}
