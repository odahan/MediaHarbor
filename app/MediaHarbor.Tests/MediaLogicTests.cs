using System.IO;
using Xunit;

namespace MediaHarbor.Tests;

public class MediaLogicTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=test", true)]
    [InlineData("https://youtu.be/test", true)]
    [InlineData("ytsearch1:cats", true)]
    [InlineData("https://vimeo.com/1", false)]
    [InlineData("", false)]
    public void IsValidDownloadUrl_Matches_App_Rules(string url, bool expected) =>
        Assert.Equal(expected, MediaLogic.IsValidDownloadUrl(url));

    [Fact]
    public void SplitLinks_Extracts_Unique_YouTube_Links_From_Mixed_Text()
    {
        var text = "https://youtu.be/a\nhttps://vimeo.com/ignore https://www.youtube.com/watch?v=b\nhttps://youtu.be/a";
        var links = MediaLogic.SplitLinks(text);
        Assert.Equal(2, links.Length);
        Assert.Contains("https://youtu.be/a", links);
        Assert.Contains("https://www.youtube.com/watch?v=b", links);
    }

    [Fact]
    public void SplitLinks_Returns_Empty_For_No_Valid_Links()
    {
        Assert.Empty(MediaLogic.SplitLinks("no links here"));
    }

    [Fact]
    public void PlaylistFolderName_Uses_Playlist_Title()
    {
        var results = new List<SearchResult> { new() { PlaylistTitle = "  My Mix. " } };
        Assert.Equal("My Mix", MediaLogic.PlaylistFolderName(results));
    }

    [Fact]
    public void PlaylistFolderName_Replaces_Invalid_FileName_Chars()
    {
        var results = new List<SearchResult> { new() { PlaylistTitle = "a/b\\c:d*e?f\"g<h>i|j" } };
        var name = MediaLogic.PlaylistFolderName(results);
        Assert.DoesNotContain(name, c => Path.GetInvalidFileNameChars().Contains(c));
        Assert.Equal("a_b_c_d_e_f_g_h_i_j", name);
    }

    [Fact]
    public void PlaylistFolderName_Falls_Back_To_Timestamp_When_Empty()
    {
        var name = MediaLogic.PlaylistFolderName(new List<SearchResult>());
        Assert.StartsWith("Playlist ", name);
    }

    [Fact]
    public void PlaylistFolderName_Truncates_Long_Names()
    {
        var longTitle = new string('x', 200);
        var results = new List<SearchResult> { new() { PlaylistTitle = longTitle } };
        Assert.Equal(120, MediaLogic.PlaylistFolderName(results).Length);
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("12.5", true)]
    [InlineData("01:30", true)]
    [InlineData("00:01:30", true)]
    [InlineData("-5", false)]
    [InlineData("-00:01:00", false)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    public void ValidTime_Accepts_Seconds_Or_TimeSpan(string text, bool expected) =>
        Assert.Equal(expected, MediaLogic.ValidTime(text));

    [Fact]
    public void SanitizeFileName_Replaces_Invalid_Chars_And_Falls_Back()
    {
        Assert.Equal("a_b", MediaLogic.SanitizeFileName("a/b"));
        Assert.Equal("Untitled", MediaLogic.SanitizeFileName("   "));
    }
}
