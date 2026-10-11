using MyDownloader.Core;
using Xunit;

namespace MyDownloader.Tests;

public class DownloadConfigFactoryTests
{
    [Fact]
    public void 默认配置_分块与并行符合约定()
    {
        var config = DownloadConfigFactory.Create(useSystemProxy: false);
        Assert.Equal(8, config.ChunkCount);
        Assert.True(config.ParallelDownload);
        Assert.Equal(5, config.MaxTryAgainOnFailover);
        Assert.True(config.CheckDiskSizeBeforeDownload);
    }

    [Fact]
    public void 启用系统代理_Proxy非空且凭据为默认凭据()
    {
        var config = DownloadConfigFactory.Create(useSystemProxy: true);
        Assert.NotNull(config.RequestConfiguration.Proxy);
        Assert.Same(System.Net.CredentialCache.DefaultCredentials, config.RequestConfiguration.Proxy.Credentials);
    }

    [Fact]
    public void 禁用系统代理_Proxy为空()
    {
        var config = DownloadConfigFactory.Create(useSystemProxy: false);
        Assert.Null(config.RequestConfiguration.Proxy);
    }

    [Fact]
    public void 不设置服务器凭据()
    {
        var config = DownloadConfigFactory.Create(useSystemProxy: true);
        Assert.Null(config.RequestConfiguration.Credentials);
    }
}
