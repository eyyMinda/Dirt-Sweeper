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
        [DataMember] public int FpsOverride;          // 0 = use the monitor's refresh rate
        [DataMember] public bool OnlyWhenFocused;
        [DataMember] public bool ShowHud;
        [DataMember] public double HudFx, HudFy;      // HUD position as a fraction of the game window

        // sweeper
        [DataMember] public double Stat, PxPerStat;   // in-game collision radius and px of circle diameter per point
        [DataMember] public int Spacing;              // % of the radius trusted for spot spacing
        [DataMember] public int HoldFrames;           // game frames spent on each spot
        [DataMember] public double AreaLeft, AreaTop, AreaRight, AreaBottom;
        [DataMember] public bool Columns, Shift, PingPong, PauseOnMouse;

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
            FpsOverride = 0; OnlyWhenFocused = true; ShowHud = true; HudFx = 0.006; HudFy = 0.945;
            Stat = 40; PxPerStat = 6.6; Spacing = 100; HoldFrames = 2;
            AreaLeft = 0; AreaTop = 0; AreaRight = 1480; AreaBottom = 1080;
            Columns = false; Shift = true; PingPong = true; PauseOnMouse = true;
            TimerX = 1632; TimerY = 312; TimerW = 180; TimerH = 66; ButtonX = 1809; ButtonY = 306;
            Levels = "1,2,3"; OffsetMs = -5; MinRunMs = 500; TextMin = 110; ResumeAfter = true;
        }

        public static string FilePath { get; } = PickPath();

        static string PickPath()
        {
            const string name = "DirtSweeper.settings.json";
            string local = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), name);
            try
            {
                using (File.Open(local, FileMode.OpenOrCreate, FileAccess.ReadWrite)) { }
                return local;
            }
            catch (Exception)
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DirtSweeper");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, name);
            }
        }

        static readonly DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Settings));

        public static Settings Load()
        {
            try
            {
                using (var f = File.OpenRead(FilePath))
                    if (f.Length > 0) return (Settings)serializer.ReadObject(f);
            }
            catch (Exception) { }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                using (var f = File.Create(FilePath))
                using (var w = JsonReaderWriterFactory.CreateJsonWriter(f, Encoding.UTF8, false, true, "  "))
                    serializer.WriteObject(w, this);
            }
            catch (Exception) { }
        }
    }
}
