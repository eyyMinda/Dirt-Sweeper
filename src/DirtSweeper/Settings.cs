using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Forms;

namespace DirtSweeper
{
    /// <summary>
    /// Everything the user can change. Positions and sizes are in the game's 1920x1080
    /// layout and get scaled to the real window (see <see cref="GameLayout"/>).
    /// </summary>
    [DataContract]
    sealed class Settings
    {
        // general
        [DataMember] public string GameProcess;
        [DataMember] public string GamePath;          // remembered when the game is seen running, for "Launch game"
        [DataMember] public bool OnlyWhenFocused;
        [DataMember] public bool ShowHud;
        [DataMember] public double HudFx, HudFy;      // HUD position as a fraction of the game window
        [DataMember] public bool ShowGuides;
        [DataMember] public int Tab;                  // last open tab of the settings panel
        [DataMember] public bool CheckUpdates;        // check GitHub for a newer release at startup and daily
        [DataMember] public long LastUpdateCheck;     // UTC ticks of the last check

        // sweeper
        [DataMember] public double Stat, PxPerStat;   // in-game collision radius and px of circle diameter per point
        [DataMember] public int Spacing;              // % of the radius trusted for spot spacing
        [DataMember] public int HoldMs;               // time spent on each spot
        [DataMember] public double AreaLeft, AreaTop, AreaRight, AreaBottom;
        [DataMember] public bool Shift, PingPong, PauseOnMouse;

        // timer minigame
        [DataMember] public double TimerX, TimerY, TimerW, TimerH, ButtonX, ButtonY;
        [DataMember] public string Levels;
        [DataMember] public int OffsetMs, MinRunMs, TextMin;
        [DataMember] public bool ResumeAfter;

        public Settings() { SetDefaults(); }

        // DataContract skips constructors, so fields missing from an older file get defaults here
        [OnDeserializing] void OnDeserializing(StreamingContext _) { SetDefaults(); }

        public void SetDefaults()
        {
            GameProcess = "Dirt Clicker"; GamePath = "";
            OnlyWhenFocused = true; ShowHud = true; HudFx = 0.006; HudFy = 0.945; ShowGuides = false; Tab = 0;
            CheckUpdates = true; LastUpdateCheck = 0;
            // a fresh game: collision radius level 1, timer levels 1-3. px/level and hold tuned on 1920x1080 at 144 FPS
            Stat = 1; PxPerStat = 6.4; Spacing = 100; HoldMs = 15;
            AreaLeft = 0; AreaTop = 0; AreaRight = 1485; AreaBottom = 1080;
            Shift = true; PingPong = true; PauseOnMouse = true;
            TimerX = 1632; TimerY = 312; TimerW = 180; TimerH = 66; ButtonX = 1809; ButtonY = 306;
            Levels = "1,2,3"; OffsetMs = -5; MinRunMs = 500; TextMin = 110; ResumeAfter = true;
        }

        // ---------- storage ----------
        // %LOCALAPPDATA%\DirtSweeper\settings.json, like SteamRouteTool. Removing the app = deleting the exe and that folder.

        public static readonly string DefaultDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DirtSweeper");

        public static string FilePath { get; private set; } = Path.Combine(DefaultDirectory, "settings.json");
        static bool customPath;

        /// <summary>Where 0.1.0 and earlier kept the settings: next to the exe, or %AppData% when that wasn't writable.</summary>
        static readonly string[] LegacyFiles =
        {
            Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "DirtSweeper.settings.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DirtSweeper", "DirtSweeper.settings.json"),
        };

        /// <summary>Use another settings file (dev builds: <c>--settings path</c>), so testing never touches the real one.</summary>
        public static void UseFile(string path)
        {
            FilePath = Path.GetFullPath(path);
            customPath = true;
        }

        /// <summary>Set when this start moved an old settings file into the new location.</summary>
        public static string MigratedFrom { get; private set; }
        /// <summary>Set when the file couldn't be read (it is kept as settings.broken.json).</summary>
        public static string LoadProblem { get; private set; }

        static readonly DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Settings));

        static Settings Read(string path)
        {
            using (var f = File.OpenRead(path))
                return f.Length > 0 ? (Settings)serializer.ReadObject(f) : null;
        }

        public static Settings Load()
        {
            if (!File.Exists(FilePath) && !customPath)
            {
                var migrated = Migrate();
                if (migrated != null) return migrated;
            }
            try
            {
                if (File.Exists(FilePath)) return Read(FilePath) ?? new Settings();
            }
            catch (Exception)
            {
                // keep the unreadable file (e.g. a typo from editing it by hand) instead of silently overwriting it
                try { File.Copy(FilePath, Path.Combine(Path.GetDirectoryName(FilePath), "settings.broken.json"), true); } catch (Exception) { }
                LoadProblem = "the settings file couldn't be read, so defaults are used (old file kept as settings.broken.json)";
            }
            return new Settings();
        }

        /// <summary>Moves the newest pre-0.1.1 settings file into the new location and deletes the old one.</summary>
        static Settings Migrate()
        {
            string source = null;
            foreach (var f in LegacyFiles)
                if (File.Exists(f) && new FileInfo(f).Length > 0 && (source == null || File.GetLastWriteTimeUtc(f) > File.GetLastWriteTimeUtc(source)))
                    source = f;
            if (source == null) return null;
            Settings s;
            try { s = Read(source); }
            catch (Exception) { return null; }
            if (s == null || !s.Save()) return null;
            try
            {
                File.Delete(source);
                string dir = Path.GetDirectoryName(source);
                if (dir != Path.GetDirectoryName(Application.ExecutablePath) && Directory.GetFileSystemEntries(dir).Length == 0)
                    Directory.Delete(dir);
            }
            catch (Exception) { }
            MigratedFrom = source;
            return s;
        }

        /// <summary>Writes the settings (to a temp file first, so a crash never leaves a half-written file). False on failure.</summary>
        public bool Save()
        {
            string tmp = FilePath + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                using (var f = File.Create(tmp))
                using (var w = JsonReaderWriterFactory.CreateJsonWriter(f, Encoding.UTF8, false, true, "  "))
                    serializer.WriteObject(w, this);
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
                return true;
            }
            catch (Exception)
            {
                try { File.Delete(tmp); } catch (Exception) { }
                return false;
            }
        }
    }
}
