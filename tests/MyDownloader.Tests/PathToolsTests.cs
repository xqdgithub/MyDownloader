using MyDownloader.Core;
using Xunit;

namespace MyDownloader.Tests;

public class PathToolsTests
{
    [Theory]
    [InlineData("https://a.com/dir/file.zip", "file.zip")]
    [InlineData("https://a.com/dir/file.zip?token=1", "file.zip")]
    [InlineData("https://a.com/", "")]
    public void 从URL推导文件名(string url, string expected)
    {
        Assert.Equal(expected, PathTools.ExtractFileName(url));
    }

    [Fact]
    public void 无文件名时返回默认名()
    {
        var name = PathTools.BuildFileName("https://a.com/", DateTime.Parse("2026-01-02 03:04:05"));
        Assert.Equal("download_20260102030405", name);
    }

    [Fact]
    public void 同名文件自动加序号()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mdtest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var target = Path.Combine(dir, "a.zip");
            File.WriteAllText(target, "x");
            var result = PathTools.EnsureUniquePath(target);
            Assert.Equal(Path.Combine(dir, "a(1).zip"), result);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void 格式化速度()
    {
        Assert.Equal("1.00MB/s", PathTools.FormatSpeed(1024 * 1024));
        Assert.Equal("--", PathTools.FormatSpeed(0));
    }

    [Fact]
    public void 格式化剩余时间()
    {
        Assert.Equal("--", PathTools.FormatEta(1000, 0));
        Assert.Equal("00:00:10", PathTools.FormatEta(1024 * 1024, 1024 * 1024 / 10));
    }

    [Fact]
    public void 格式化剩余时间_溢出或负值返回占位()
    {
        Assert.Equal("--", PathTools.FormatEta(long.MaxValue, 0.1));
        Assert.Equal("--", PathTools.FormatEta(-5, 100));
    }

    [Theory]
    [InlineData("https://a.com/file.zip", true)]
    [InlineData("https://a.com/file.exe?x=1", true)]
    [InlineData("http://a.com/a/b/Video.MP4", true)]
    [InlineData("https://a.com/download", false)]      // 无扩展名的网页
    [InlineData("https://a.com/page.html", false)]     // html 网页
    [InlineData("https://obsidian.md/download", false)] // 普通网页
    [InlineData("ftp://a.com/file.zip", false)]        // 非 http
    [InlineData("not a url", false)]
    [InlineData("", false)]
    public void 判断是否像下载链接(string url, bool expected)
    {
        Assert.Equal(expected, PathTools.LooksLikeDownloadUrl(url));
    }
}
