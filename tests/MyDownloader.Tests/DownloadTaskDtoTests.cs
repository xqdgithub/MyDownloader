using System.Text.Json;
using Downloader;
using MyDownloader.Core;
using Xunit;

namespace MyDownloader.Tests;

public class DownloadTaskDtoTests
{
    [Fact]
    public void 序列化往返_保留分块位置与重试参数()
    {
        var dto = new DownloadTaskDto
        {
            Id = "abc",
            Url = "https://example.com/a.zip",
            FilePath = @"C:\tmp\a.zip",
            Status = DownloadStatus.Paused,
            TotalFileSize = 1000,
            FileName = @"C:\tmp\a.zip",
            Urls = new[] { "https://example.com/a.zip" },
            IsSupportDownloadInRange = true,
            Chunks = new[]
            {
                new ChunkDto { Start = 0, End = 499, Position = 250, MaxTryAgainOnFailover = 5, Timeout = 1000 },
                new ChunkDto { Start = 500, End = 999, Position = 0, MaxTryAgainOnFailover = 5, Timeout = 1000 },
            }
        };

        var json = JsonSerializer.Serialize(dto);
        var back = JsonSerializer.Deserialize<DownloadTaskDto>(json)!;

        Assert.Equal("abc", back.Id);
        Assert.Equal(2, back.Chunks!.Length);
        Assert.Equal(250, back.Chunks[0].Position);
        Assert.Equal(5, back.Chunks[0].MaxTryAgainOnFailover);
        Assert.Equal(1000, back.Chunks[0].Timeout);
        Assert.True(back.IsSupportDownloadInRange);
    }

    [Fact]
    public void 序列化_不包含Storage字段()
    {
        var dto = new DownloadTaskDto { Id = "x" };
        var json = JsonSerializer.Serialize(dto);
        Assert.DoesNotContain("Storage", json);
        Assert.DoesNotContain("ConcurrentStream", json);
    }

    [Fact]
    public void 转DownloadPackage_分块已还原且Storage为空()
    {
        var dto = new DownloadTaskDto
        {
            TotalFileSize = 100,
            FileName = @"C:\tmp\b.bin",
            Chunks = new[] { new ChunkDto { Start = 0, End = 99, Position = 40, MaxTryAgainOnFailover = 5, Timeout = 1000 } }
        };

        var package = dto.ToPackage();

        Assert.NotNull(package.Chunks);
        Assert.Equal(40, package.Chunks![0].Position);
        Assert.Null(package.Storage);
        Assert.Equal(100, package.TotalFileSize);
    }

    [Fact]
    public void FromTaskInfo_空分块时Chunks为空()
    {
        var info = new DownloadTaskInfo
        {
            ID = "id1",
            Url = "https://e.com/f",
            FilePath = @"C:\tmp\f",
            DownloadPackage = new DownloadPackage { Status = DownloadStatus.None, TotalFileSize = 0 }
        };

        var dto = DownloadTaskDto.FromTaskInfo(info);

        Assert.Equal("id1", dto.Id);
        Assert.Null(dto.Chunks);
    }
}
