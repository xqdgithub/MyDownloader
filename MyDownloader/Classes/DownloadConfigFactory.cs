using Downloader;
using System.Net;

namespace MyDownloader.Classes
{
    /// <summary>
    /// 统一构建下载配置，保证所有任务配置一致。
    /// </summary>
    public static class DownloadConfigFactory
    {
        /// <summary>
        /// 创建下载配置。
        /// </summary>
        /// <param name="useSystemProxy">是否跟随系统代理（默认开启）。</param>
        public static DownloadConfiguration Create(bool useSystemProxy = true)
        {
            var option = DownloadTools.GetDefaultDowmloadOptions(null);
            option.ChunkCount = 8;
            option.ParallelDownload = true;
            option.MaxTryAgainOnFailover = 5;
            option.CheckDiskSizeBeforeDownload = true;

            if (useSystemProxy)
            {
                var proxy = WebRequest.GetSystemWebProxy();
                proxy.Credentials = CredentialCache.DefaultCredentials;
                option.RequestConfiguration.Proxy = proxy;
                // 注意：不设置 RequestConfiguration.Credentials（那是服务器认证，不是代理认证）
            }

            return option;
        }
    }
}
