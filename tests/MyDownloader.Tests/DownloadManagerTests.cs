using MyDownloader.Core;
using Xunit;

namespace MyDownloader.Tests;

public class DownloadManagerTests
{
    [Fact]
    public void 单例唯一()
    {
        Assert.Same(DownloadManager.Instance, DownloadManager.Instance);
    }

    [Fact]
    public void 空管理器_快照不为空集合()
    {
        var snapshots = DownloadManager.Instance.GetSnapshots();
        Assert.NotNull(snapshots);
    }
}
