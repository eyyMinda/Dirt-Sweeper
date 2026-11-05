using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;

namespace DirtSweeper
{
    static class SweepPath
    {
        public static double CircleDiameter(Settings s, GameLayout lay) => lay.Px(s.Stat * s.PxPerStat);

        /// <summary>Play area in screen pixels (inclusive edges).</summary>
        public static RectangleF Area(Settings s, GameLayout lay)
        {
            double l = lay.X(s.AreaLeft), t = lay.Y(s.AreaTop);
            double r = Math.Max(l, lay.XRight(s.AreaRight) - 1), b = Math.Max(t, lay.Y(s.AreaBottom) - 1);
            return RectangleF.FromLTRB((float)l, (float)t, (float)r, (float)b);
        }

        /// <summary>
        /// Snake path over a hex grid of spots. A hex grid with spot spacing sqrt(3)*r and row
        /// spacing 1.5*r leaves no gaps for circles of radius r, with the fewest spots.
        /// <paramref name="rnd"/> shifts the grid randomly; null picks the shift needing fewest spots.
        /// </summary>
        public static List<Point> Build(Settings s, GameLayout lay, Random rnd)
        {
            double r = Math.Max(2, CircleDiameter(s, lay) / 2 * s.Spacing / 100);
            var a = Area(s, lay);
            double L = a.Left, T = a.Top, R = a.Right, B = a.Bottom;
            if (s.Columns) { (L, T) = (T, L); (R, B) = (B, R); }

            List<List<Point>> rows;
            if (rnd != null) rows = Hex(L, T, R, B, r, rnd.NextDouble(), rnd.NextDouble());
            else
            {
                rows = null;
                for (int i = 0; i < 6; i++)
                    for (int j = 0; j < 6; j++)
                    {
                        var c = Hex(L, T, R, B, r, i / 6.0, j / 6.0);
                        if (rows == null || Count(c) < Count(rows)) rows = c;
                    }
            }

            var pts = new List<Point>();
            for (int k = 0; k < rows.Count; k++)
            {
                if (k % 2 == 1) rows[k].Reverse();
                pts.AddRange(rows[k]);
            }
            if (s.Columns)
                for (int i = 0; i < pts.Count; i++) pts[i] = new Point(pts[i].Y, pts[i].X);
            return pts;
        }

        static int Count(List<List<Point>> rows)
        {
            int n = 0;
            foreach (var r in rows) n += r.Count;
            return n;
        }

        // Rows of lattice spots whose circle touches the area, clamped into it. Clamping a spot
        // into the area never moves it away from any point of the area, so coverage holds.
        static List<List<Point>> Hex(double L, double T, double R, double B, double r, double phx, double phy)
        {
            double dx = Math.Sqrt(3) * r, dy = 1.5 * r, ox = L + phx * dx, oy = T + phy * dy;
            var rows = new List<List<Point>>();
            int j0 = (int)Math.Floor((T - r - oy) / dy), j1 = (int)Math.Ceiling((B + r - oy) / dy);
            for (int j = j0; j <= j1; j++)
            {
                double y = oy + j * dy, off = ox + ((j & 1) != 0 ? dx / 2 : 0);
                var row = new List<Point>();
                int i0 = (int)Math.Floor((L - r - off) / dx), i1 = (int)Math.Ceiling((R + r - off) / dx);
                for (int i = i0; i <= i1; i++)
                {
                    double x = off + i * dx;
                    double nx = Math.Min(Math.Max(x, L), R), ny = Math.Min(Math.Max(y, T), B);
                    if ((x - nx) * (x - nx) + (y - ny) * (y - ny) > r * r) continue;
                    var p = new Point((int)Math.Round(nx), (int)Math.Round(ny));
                    if (row.Count == 0 || Math.Abs(row[row.Count - 1].X - p.X) + Math.Abs(row[row.Count - 1].Y - p.Y) > 2)
                        row.Add(p);
                }
                if (row.Count > 0) rows.Add(row);
            }
            return rows;
        }
    }

    /// <summary>Moves the cursor spot to spot, holding each for a few game frames.</summary>
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
        public double Fps => s.FpsOverride > 0 ? s.FpsOverride : game.Current.RefreshHz;
        public double HoldSeconds => s.HoldFrames / Fps;

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
            var path = SweepPath.Build(s, g.Layout, s.Shift ? rnd : null);
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
