using System.Collections.Generic;
using System.Drawing;
using Xunit;

namespace DirtSweeper.Tests
{
    public class SweepPathTests
    {
        // the default dig field on 1920x1080: 0,0 -> 1485,1080 (inclusive pixel edges)
        const float L = 0, T = 0, R = 1484, B = 1079;

        /// <summary>Counts pixels of the area no spot's circle reaches (exact, every pixel).</summary>
        static int Gaps(List<Point> spots, float l, float t, float r, float b, double radius)
        {
            double rr = radius * radius;
            int gaps = 0;
            for (int y = (int)t; y <= (int)b; y++)
                for (int x = (int)l; x <= (int)r; x++)
                {
                    bool hit = false;
                    foreach (var p in spots)
                    {
                        double dx = p.X - x, dy = p.Y - y;
                        if (dx * dx + dy * dy <= rr) { hit = true; break; }
                    }
                    if (!hit) gaps++;
                }
            return gaps;
        }

        [Theory]
        [InlineData(66)]   // collision radius ~20 at 6.6 px/level
        [InlineData(132)]
        [InlineData(185)]  // level 56: the default
        [InlineData(265)]
        [InlineData(400)]
        public void Fit_covers_every_pixel(double radius)
        {
            var spots = SweepPath.Fit(L, T, R, B, radius);
            Assert.Equal(0, Gaps(spots, L, T, R, B, radius));
        }

        [Fact]
        public void Mirrored_path_still_covers_every_pixel()
        {
            const double radius = 185;
            var spots = SweepPath.Fit(L, T, R, B, radius);
            var area = RectangleF.FromLTRB(L, T, R, B);
            foreach (var (mx, my) in new[] { (true, false), (false, true), (true, true) })
            {
                var m = new List<Point>(spots);
                SweepPath.Mirror(m, area, mx, my);
                Assert.Equal(0, Gaps(m, L, T, R, B, radius));
            }
        }

        [Fact]
        public void Default_field_at_level_56_needs_at_most_27_spots()
        {
            // regression guard: 27 spots (~0.95 s per pass at 35 ms) is what the fitter finds today
            Assert.InRange(SweepPath.Fit(L, T, R, B, 185).Count, 1, 27);
        }

        [Fact]
        public void Spots_stay_inside_the_area()
        {
            foreach (var p in SweepPath.Fit(L, T, R, B, 185))
            {
                Assert.InRange(p.X, L, R);
                Assert.InRange(p.Y, T, B);
            }
        }

        [Fact]
        public void Consecutive_spots_are_never_duplicates()
        {
            var spots = SweepPath.Fit(L, T, R, B, 132);
            for (int i = 1; i < spots.Count; i++) Assert.NotEqual(spots[i - 1], spots[i]);
        }

        [Theory]
        [InlineData(3.2)]   // level 1 at 6.4 px/level
        [InlineData(9.6)]   // level 3
        [InlineData(20)]
        public void Small_circles_cover_every_pixel(double radius)
        {
            // a corner of the field is enough: the small-circle path is one lattice, the same everywhere
            var spots = SweepPath.Fit(0, 0, 160, 120, radius);
            Assert.Equal(0, Gaps(spots, 0, 0, 160, 120, radius));
        }

        [Fact]
        public void Level_1_on_the_whole_field_fits_quickly()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var spots = SweepPath.Fit(L, T, R, B, 3.2);
            Assert.True(sw.Elapsed.TotalSeconds < 3, $"took {sw.Elapsed.TotalSeconds:0.0} s");
            Assert.InRange(spots.Count, 10000, 400000);
        }

        [Fact]
        public void Tiny_area_still_gets_a_spot()
        {
            Assert.NotEmpty(SweepPath.Fit(100, 100, 110, 105, 185));
        }
    }
}
