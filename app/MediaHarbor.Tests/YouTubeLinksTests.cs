using Xunit;

namespace MediaHarbor.Tests;

public class YouTubeLinksTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://music.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/playlist?list=PL12345")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("http://youtube.com/watch?v=test")]
    [InlineData("https://YOUTUBE.COM/watch?v=test")]
    public void Supported_YouTube_Links_Are_Accepted(string url) => Assert.True(YouTubeLinks.IsSupported(url));

    [Theory]
    [InlineData("https://vimeo.com/123456")]
    [InlineData("https://notyoutube.com/watch?v=test")]
    [InlineData("https://youtube.com.example.org/watch?v=test")]
    [InlineData("https://youtube.com@evil.example.org/watch")]
    [InlineData("https://user:pass@youtube.com/watch?v=test")]
    [InlineData("file:///C:/videos/test.mp4")]
    [InlineData("ftp://youtube.com/watch?v=test")]
    [InlineData("javascript:alert(1)")]
    [InlineData("youtube.com/watch?v=test")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    public void Unsupported_Links_Are_Rejected(string url) => Assert.False(YouTubeLinks.IsSupported(url));
}
