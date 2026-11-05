using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace DirtSweeper
{
    /// <summary>Brand icons Segoe MDL2 doesn't have, drawn as vectors so they stay sharp at any DPI.</summary>
    static class Icons
    {
        // GitHub's mark (viewBox 0 0 98 96); the one arc is approximated with a line, invisible at icon size
        const string GitHubMark =
            "M48.854 0C21.839 0 0 22 0 49.217c0 21.756 13.993 40.172 33.405 46.69 2.427.49 3.316-1.059 3.316-2.362 0-1.141-.08-5.052-.08-9.127" +
            "-13.59 2.934-16.42-5.867-16.42-5.867-2.184-5.704-5.42-7.17-5.42-7.17-4.448-3.015.324-3.015.324-3.015 4.934.326 7.523 5.052 7.523 5.052" +
            " 4.367 7.496 11.404 5.378 14.235 4.074.404-3.178 1.699-5.378 3.074-6.6-10.839-1.141-22.243-5.378-22.243-24.283 0-5.378 1.94-9.778" +
            " 5.014-13.2-.485-1.222-2.184-6.275.486-13.038 0 0 4.125-1.304 13.426 5.052l12.214-1.63c4.125 0 8.33.571 12.213 1.63 9.302-6.356" +
            " 13.427-5.052 13.427-5.052 2.67 6.763.97 11.816.485 13.038 3.155 3.422 5.015 7.822 5.015 13.2 0 18.905-11.404 23.06-22.324 24.283" +
            " 1.78 1.548 3.316 4.481 3.316 9.126 0 6.6-.08 11.897-.08 13.526 0 1.304.89 2.853 3.316 2.364 19.412-6.52 33.405-24.935 33.405-46.691" +
            "C97.707 22 75.788 0 48.854 0z";

        static GraphicsPath gitHub;

        public static void GitHub(Graphics g, RectangleF r, Color c)
        {
            if (gitHub == null) gitHub = ParseSvgPath(GitHubMark);
            float size = Math.Min(r.Width, r.Height), s = size / 98f;
            var state = g.Save();
            g.TranslateTransform(r.X + (r.Width - 98 * s) / 2, r.Y + (r.Height - 96 * s) / 2);
            g.ScaleTransform(s, s);
            using (var b = new SolidBrush(c)) g.FillPath(b, gitHub);
            g.Restore(state);
        }

        /// <summary>Buy Me a Coffee style cup: yellow cup with a lid and steam.</summary>
        public static void Coffee(Graphics g, RectangleF r, Color steam)
        {
            float s = Math.Min(r.Width, r.Height), x = r.X + (r.Width - s) / 2, y = r.Y + (r.Height - s) / 2;
            PointF P(float px, float py) => new PointF(x + px * s, y + py * s);
            var yellow = Color.FromArgb(255, 221, 0);
            using (var cup = new GraphicsPath())
            {
                cup.AddPolygon(new[] { P(0.22f, 0.38f), P(0.78f, 0.38f), P(0.70f, 0.96f), P(0.30f, 0.96f) });
                using (var b = new SolidBrush(yellow)) g.FillPath(b, cup);
                using (var pen = new Pen(Color.FromArgb(40, 30, 0), Math.Max(1, s * 0.06f)) { LineJoin = LineJoin.Round })
                {
                    g.DrawPath(pen, cup);
                    g.DrawLine(pen, P(0.16f, 0.34f), P(0.84f, 0.34f)); // lid
                }
            }
            using (var pen = new Pen(steam, Math.Max(1, s * 0.07f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                foreach (float sx in new[] { 0.40f, 0.60f })
                    g.DrawBezier(pen, P(sx, 0.26f), P(sx - 0.08f, 0.18f), P(sx + 0.08f, 0.10f), P(sx, 0.02f));
        }

        /// <summary>Minimal SVG path parser: M L H V C S Z (absolute and relative).</summary>
        internal static GraphicsPath ParseSvgPath(string d)
        {
            var path = new GraphicsPath(FillMode.Winding);
            var tokens = Tokenize(d);
            int i = 0;
            char cmd = 'M';
            PointF cur = PointF.Empty, start = PointF.Empty, lastCtrl = PointF.Empty;
            bool hasCtrl = false;
            float N() => tokens[i++].Num;
            while (i < tokens.Count)
            {
                if (tokens[i].Cmd != '\0') cmd = tokens[i++].Cmd;
                bool rel = char.IsLower(cmd);
                float ox = rel ? cur.X : 0, oy = rel ? cur.Y : 0;
                switch (char.ToUpperInvariant(cmd))
                {
                    case 'M':
                        cur = start = new PointF(ox + N(), oy + N());
                        path.StartFigure();
                        cmd = rel ? 'l' : 'L'; // further pairs are line-tos
                        hasCtrl = false;
                        break;
                    case 'L':
                    {
                        var p = new PointF(ox + N(), oy + N());
                        path.AddLine(cur, p); cur = p; hasCtrl = false;
                        break;
                    }
                    case 'H': { var p = new PointF((rel ? cur.X : 0) + N(), cur.Y); path.AddLine(cur, p); cur = p; hasCtrl = false; break; }
                    case 'V': { var p = new PointF(cur.X, (rel ? cur.Y : 0) + N()); path.AddLine(cur, p); cur = p; hasCtrl = false; break; }
                    case 'C':
                    {
                        var c1 = new PointF(ox + N(), oy + N());
                        var c2 = new PointF(ox + N(), oy + N());
                        var p = new PointF(ox + N(), oy + N());
                        path.AddBezier(cur, c1, c2, p);
                        lastCtrl = c2; hasCtrl = true; cur = p;
                        break;
                    }
                    case 'S':
                    {
                        var c1 = hasCtrl ? new PointF(2 * cur.X - lastCtrl.X, 2 * cur.Y - lastCtrl.Y) : cur;
                        var c2 = new PointF(ox + N(), oy + N());
                        var p = new PointF(ox + N(), oy + N());
                        path.AddBezier(cur, c1, c2, p);
                        lastCtrl = c2; hasCtrl = true; cur = p;
                        break;
                    }
                    case 'Z':
                        path.CloseFigure(); cur = start; hasCtrl = false;
                        break;
                    default: throw new FormatException("unsupported path command " + cmd);
                }
            }
            return path;
        }

        struct Token { public char Cmd; public float Num; }

        static List<Token> Tokenize(string d)
        {
            var list = new List<Token>();
            int i = 0;
            while (i < d.Length)
            {
                char ch = d[i];
                if (char.IsLetter(ch)) { list.Add(new Token { Cmd = ch }); i++; continue; }
                if (ch == ' ' || ch == ',' || ch == '\n') { i++; continue; }
                // a number: optional sign, digits, at most one dot ("2.427.49" is 2.427 then .49)
                int s = i;
                if (ch == '-' || ch == '+') i++;
                bool dot = false;
                while (i < d.Length && (char.IsDigit(d[i]) || (d[i] == '.' && !dot)))
                {
                    if (d[i] == '.') dot = true;
                    i++;
                }
                if (i < d.Length && (d[i] == 'e' || d[i] == 'E')) { i++; if (d[i] == '-' || d[i] == '+') i++; while (i < d.Length && char.IsDigit(d[i])) i++; }
                list.Add(new Token { Num = float.Parse(d.Substring(s, i - s), CultureInfo.InvariantCulture) });
            }
            return list;
        }
    }
}
