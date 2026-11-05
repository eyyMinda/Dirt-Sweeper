using System.Drawing;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using Xunit;

namespace DirtSweeper.Tests
{
    public class SettingsAndLayoutTests
    {
        static Settings Read(string json)
        {
            var ser = new DataContractJsonSerializer(typeof(Settings));
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json))) return (Settings)ser.ReadObject(ms);
        }

        [Fact]
        public void Old_settings_file_loads_with_new_defaults_for_missing_fields()
        {
            // a 0.0.1 file: HoldFrames/FpsOverride/Columns are gone, HoldMs/ShowGuides/Tab are new
            var s = Read("{\"Stat\":40,\"PxPerStat\":6.6,\"HoldFrames\":2,\"FpsOverride\":0,\"Columns\":false,\"AreaRight\":1485}");
            Assert.Equal(40, s.Stat);
            Assert.Equal(1485, s.AreaRight);
            Assert.Equal(15, s.HoldMs);
            Assert.Equal("1,2,3", s.Levels);
        }

        [Fact]
        public void Defaults_are_a_fresh_game_on_1080p_144hz()
        {
            var s = new Settings();
            Assert.Equal(1, s.Stat);            // a new game starts at collision radius level 1
            Assert.Equal("1,2,3", s.Levels);    // and timer levels 1-3
            Assert.Equal(6.4, s.PxPerStat);
            Assert.Equal(15, s.HoldMs);
            Assert.Equal(100, s.Spacing);
            Assert.Equal(new[] { 0.0, 0, 1485, 1080 }, new[] { s.AreaLeft, s.AreaTop, s.AreaRight, s.AreaBottom });
        }

        [Fact]
        public void Layout_scales_by_height_and_anchors_the_shop_panel_right()
        {
            // 2560x1080 (21:9): scale 1, the extra 640 px go to the dig field
            var lay = new GameLayout(new Rectangle(0, 0, 2560, 1080));
            Assert.Equal(1, lay.Scale);
            Assert.Equal(640, lay.Extra);
            Assert.Equal(new Point(1632 + 640, 312), lay.Right(1632, 312));

            // 2560x1440 (16:9): everything scales by 4/3, no extra width
            lay = new GameLayout(new Rectangle(0, 0, 2560, 1440));
            Assert.Equal(new Point(2176, 416), lay.Right(1632, 312));
        }

        [Theory]
        [InlineData("1,2,3", 3)]
        [InlineData("1 2;3,4", 4)]
        [InlineData("", -1)]
        [InlineData("1,x", -1)]
        [InlineData("0", -1)]
        public void ParseLevels(string text, int count)
        {
            var levels = TimerGame.ParseLevels(text);
            if (count < 0) Assert.Null(levels);
            else Assert.Equal(count, levels.Count);
        }
    }
}
