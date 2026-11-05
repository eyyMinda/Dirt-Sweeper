using System;
using System.IO;
using Xunit;

namespace DirtSweeper.Tests
{
    // Settings.FilePath is static; keep every test that changes it in this one class (xUnit runs a class's tests one by one).
    public class SettingsStorageTests : IDisposable
    {
        readonly string dir = Path.Combine(Path.GetTempPath(), "DirtSweeperTests-" + Guid.NewGuid().ToString("N"));

        public SettingsStorageTests() => Directory.CreateDirectory(dir);

        public void Dispose()
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }

        [Fact]
        public void Default_location_is_local_app_data()
        {
            string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DirtSweeper");
            Assert.Equal(expected, Settings.DefaultDirectory);
        }

        [Fact]
        public void Saves_and_loads_back_including_a_new_folder()
        {
            string file = Path.Combine(dir, "sub", "settings.json");
            Settings.UseFile(file);
            var s = new Settings { Stat = 48, PxPerStat = 6.44, HoldMs = 15, Levels = "1,2,3,4" };
            Assert.True(s.Save());
            Assert.False(File.Exists(file + ".tmp"));

            var back = Settings.Load();
            Assert.Equal(48, back.Stat);
            Assert.Equal(6.44, back.PxPerStat);
            Assert.Equal(15, back.HoldMs);
            Assert.Equal("1,2,3,4", back.Levels);

            // saving over an existing file replaces it
            back.Stat = 49;
            Assert.True(back.Save());
            Assert.Equal(49, Settings.Load().Stat);
        }

        [Fact]
        public void Unreadable_file_gives_defaults_and_is_kept_aside()
        {
            string file = Path.Combine(dir, "settings.json");
            File.WriteAllText(file, "{ \"Stat\": 48, oops");
            Settings.UseFile(file);
            var s = Settings.Load();
            Assert.Equal(new Settings().Stat, s.Stat);
            Assert.True(File.Exists(Path.Combine(dir, "settings.broken.json")));
            Assert.NotNull(Settings.LoadProblem);
        }

        [Fact]
        public void Missing_custom_file_gives_defaults()
        {
            Settings.UseFile(Path.Combine(dir, "none.json"));
            Assert.Equal(new Settings().HoldMs, Settings.Load().HoldMs);
        }
    }
}
