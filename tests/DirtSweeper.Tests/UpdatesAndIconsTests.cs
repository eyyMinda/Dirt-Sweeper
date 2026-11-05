using System;
using Xunit;

namespace DirtSweeper.Tests
{
    public class UpdatesAndIconsTests
    {
        const string Release = @"{
  ""tag_name"": ""v9.8.7"", ""html_url"": ""https://github.com/eyyMinda/Dirt-Sweeper/releases/tag/v9.8.7"",
  ""draft"": false, ""prerelease"": false, ""body"": ""notes"", ""unknown_field"": {""x"": 1},
  ""assets"": [
    { ""name"": ""DirtSweeper-9.8.7.exe"", ""browser_download_url"": ""https://example.invalid/DirtSweeper-9.8.7.exe"", ""size"": 100000 },
    { ""name"": ""SHA256SUMS.txt"", ""browser_download_url"": ""https://example.invalid/SHA256SUMS.txt"" }
  ]
}";

        [Fact]
        public void Parses_a_github_release()
        {
            var r = Updates.Parse(Release);
            Assert.Equal(new Version(9, 8, 7), r.Version);
            Assert.Equal("DirtSweeper-9.8.7.exe", r.ExeAsset);
            Assert.True(r.CanInstall);
            Assert.True(r.IsNewer);
        }

        [Fact]
        public void Release_without_the_exe_cant_be_installed_in_place()
        {
            var r = Updates.Parse(Release.Replace("DirtSweeper-9.8.7.exe", "Something-else.exe"));
            Assert.False(r.CanInstall);
        }

        [Fact]
        public void Drafts_and_prereleases_are_ignored()
        {
            Assert.Null(Updates.Parse(Release.Replace("\"prerelease\": false", "\"prerelease\": true")));
            Assert.Null(Updates.Parse(Release.Replace("\"draft\": false", "\"draft\": true")));
        }

        [Theory]
        [InlineData("v0.1.0", "0.1.0")]
        [InlineData("1.2", "1.2.0")]
        [InlineData("V2.0.5", "2.0.5")]
        public void Tags_become_versions(string tag, string expected)
        {
            Assert.True(Updates.TryVersion(tag, out var v));
            Assert.Equal(Version.Parse(expected), v);
        }

        [Theory]
        [InlineData("")]
        [InlineData("latest")]
        [InlineData("v1.x")]
        public void Bad_tags_are_rejected(string tag) => Assert.False(Updates.TryVersion(tag, out _));

        [Fact]
        public void Running_version_is_three_part_and_not_newer_than_itself()
        {
            Assert.Equal(-1, AppInfo.Version.Revision);
            Assert.False(new ReleaseInfo { Version = AppInfo.Version }.IsNewer);
        }

        [Fact]
        public void Finds_the_checksum_for_a_file()
        {
            const string sums = "ABCDEF01  DirtSweeper-1.0.0.exe\r\n0123abcd *other.exe\n";
            Assert.Equal("abcdef01", Updates.FindChecksum(sums, "DirtSweeper-1.0.0.exe"));
            Assert.Equal("0123abcd", Updates.FindChecksum(sums, "other.exe"));
            Assert.Null(Updates.FindChecksum(sums, "missing.exe"));
        }

        [Fact]
        public void GitHub_mark_parses_to_its_viewbox()
        {
            // the full official path, numbers like "2.427.49" and implicit repeats included
            var b = Icons.ParseSvgPath("M48.854 0C21.839 0 0 22 0 49.217c0 21.756 13.993 40.172 33.405 46.69 2.427.49 3.316-1.059 3.316-2.362C97.707 22 75.788 0 48.854 0z").GetBounds();
            Assert.InRange(b.Left, -0.5f, 0.5f);
            Assert.InRange(b.Top, -0.5f, 0.5f);
            Assert.InRange(b.Right, 90f, 98.5f);
            Assert.InRange(b.Bottom, 90f, 96.5f);
        }
    }
}
