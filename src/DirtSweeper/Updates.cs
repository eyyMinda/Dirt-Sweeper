using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace DirtSweeper
{
    static class AppInfo
    {
        public const string Repo = "eyyMinda/Dirt-Sweeper";
        public const string RepoUrl = "https://github.com/" + Repo;
        public const string IssuesUrl = RepoUrl + "/issues/new/choose";
        public const string ReleasesUrl = RepoUrl + "/releases/latest";
        public const string AuthorUrl = "https://github.com/eyyMinda";
        public const string BuyMeACoffeeUrl = "https://buymeacoffee.com/eyyminda";
        public const string WebsiteUrl = "https://eyyminda.github.io/Dirt-Sweeper/";

        public static readonly Version Version = Trim(Assembly.GetExecutingAssembly().GetName().Version);
        public static string VersionText => Version.ToString(3);

        static Version Trim(Version v) => new Version(v.Major, v.Minor, Math.Max(0, v.Build));

        public static void Open(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception) { }
        }
    }

    /// <summary>The latest GitHub release, as far as the updater cares.</summary>
    sealed class ReleaseInfo
    {
        public Version Version;
        public string Tag, PageUrl;
        public readonly Dictionary<string, string> Assets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string ExeAsset => $"DirtSweeper-{Version.ToString(3)}.exe";
        public bool IsNewer => Version > AppInfo.Version;
        public bool CanInstall => Assets.ContainsKey(ExeAsset) && Assets.ContainsKey(Updates.ChecksumsFile);
    }

    sealed class UpdateException : Exception
    {
        public UpdateException(string message) : base(message) { }
    }

    /// <summary>
    /// Checks GitHub for a newer release and installs it: downloads the exe next to the running one, checks it against
    /// the release's SHA256SUMS.txt, then swaps it in. Windows lets a running exe be renamed but not overwritten, so the
    /// current exe becomes .old (deleted on the next start) and the new one takes its name. The only network traffic
    /// the app ever makes.
    /// </summary>
    static class Updates
    {
        public const string ChecksumsFile = "SHA256SUMS.txt";

#pragma warning disable CS0649 // filled in by the JSON serializer
        [DataContract] sealed class ApiAsset { [DataMember(Name = "name")] public string Name; [DataMember(Name = "browser_download_url")] public string Url; }
        [DataContract]
        sealed class ApiRelease
        {
            [DataMember(Name = "tag_name")] public string Tag;
            [DataMember(Name = "html_url")] public string Url;
            [DataMember(Name = "draft")] public bool Draft;
            [DataMember(Name = "prerelease")] public bool Prerelease;
            [DataMember(Name = "assets")] public ApiAsset[] Assets;
        }
#pragma warning restore CS0649

        static WebClient Client()
        {
            // .NET Framework may not offer TLS 1.2 by default on every Windows build; GitHub requires it
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var c = new WebClient { Encoding = Encoding.UTF8 };
            c.Headers[HttpRequestHeader.UserAgent] = "DirtSweeper/" + AppInfo.VersionText;
            c.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            return c;
        }

        /// <summary>The latest published release (blocking; call off the UI thread). Null when there is none yet.</summary>
        public static ReleaseInfo FetchLatest(string repo = AppInfo.Repo)
        {
            string json;
            try
            {
                using (var c = Client()) json = c.DownloadString($"https://api.github.com/repos/{repo}/releases/latest");
            }
            catch (WebException ex) when ((ex.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
            {
                return null; // no release published yet
            }
            catch (WebException ex) { throw new UpdateException("couldn't reach GitHub (" + ex.Status + ")"); }
            return Parse(json);
        }

        internal static ReleaseInfo Parse(string json)
        {
            ApiRelease r;
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                r = (ApiRelease)new DataContractJsonSerializer(typeof(ApiRelease)).ReadObject(ms);
            if (r == null || r.Draft || r.Prerelease || !TryVersion(r.Tag, out var v)) return null;
            var info = new ReleaseInfo { Version = v, Tag = r.Tag, PageUrl = r.Url ?? AppInfo.ReleasesUrl };
            foreach (var a in r.Assets ?? new ApiAsset[0])
                if (a?.Name != null && a.Url != null) info.Assets[a.Name] = a.Url;
            return info;
        }

        internal static bool TryVersion(string tag, out Version v)
        {
            v = null;
            if (string.IsNullOrEmpty(tag)) return false;
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var p)) return false;
            v = new Version(p.Major, p.Minor, Math.Max(0, p.Build));
            return true;
        }

        /// <summary>The SHA-256 (lowercase hex) listed for a file in SHA256SUMS.txt ("&lt;hash&gt;  &lt;name&gt;" per line), or null.</summary>
        internal static string FindChecksum(string sums, string fileName)
        {
            foreach (string line in (sums ?? "").Split('\n'))
            {
                var parts = line.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && parts[1].Trim().TrimStart('*').Equals(fileName, StringComparison.OrdinalIgnoreCase))
                    return parts[0].ToLowerInvariant();
            }
            return null;
        }

        internal static string Sha256(string path)
        {
            using (var f = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(f)).Replace("-", "").ToLowerInvariant();
        }

        static string ExePath => Application.ExecutablePath;

        /// <summary>Downloads, verifies and swaps in the new exe (blocking). Afterwards call <see cref="Restart"/>.</summary>
        public static void Install(ReleaseInfo release)
        {
            if (!release.CanInstall) throw new UpdateException("the release has no " + release.ExeAsset);
            string exe = ExePath, newPath = exe + ".new", oldPath = exe + ".old";
            try
            {
                using (var c = Client())
                {
                    string expected = FindChecksum(c.DownloadString(release.Assets[ChecksumsFile]), release.ExeAsset);
                    if (expected == null) throw new UpdateException("no checksum for " + release.ExeAsset);
                    c.DownloadFile(release.Assets[release.ExeAsset], newPath);
                    if (Sha256(newPath) != expected) throw new UpdateException("the download didn't match its checksum");
                }
                if (File.Exists(oldPath)) File.Delete(oldPath);
                File.Move(exe, oldPath);
                try { File.Move(newPath, exe); }
                catch (Exception) { File.Move(oldPath, exe); throw; }
            }
            catch (Exception ex) when (ex is WebException || ex is IOException || ex is UnauthorizedAccessException)
            {
                TryDelete(newPath);
                throw new UpdateException(ex is UnauthorizedAccessException
                    ? "can't write next to the exe; download it from GitHub instead"
                    : "update failed: " + ex.Message);
            }
            catch (UpdateException) { TryDelete(newPath); throw; }
        }

        /// <summary>Starts the (new) exe, which waits for this process to exit before taking over.</summary>
        public static void Restart()
        {
            Process.Start(new ProcessStartInfo(ExePath, "--wait-pid " + Process.GetCurrentProcess().Id)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(ExePath),
            });
        }

        /// <summary>Removes what the last update left behind (the old exe can only go once it's no longer running).</summary>
        public static void CleanUp()
        {
            TryDelete(ExePath + ".old");
            TryDelete(ExePath + ".new");
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception) { }
        }
    }
}
