using Downloader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MyDownloader.Classes
{
    public class DownloadTools
    {
       
        public static DownloadConfiguration GetDefaultDowmloadOptions(CookieContainer? cookie)
        {
            var downloadOpt = new DownloadConfiguration()
            {
                // usually, hosts support max to 8000 bytes, default value is 8000
                //BufferBlockSize = 10240,
                // file parts to download, the default value is 1
                ChunkCount = 8,
                // download speed limited to 2MB/s, default values is zero or unlimited
                //MaximumBytesPerSecond = 1024 * 1024 * 2,
                // the maximum number of times to fail
                MaxTryAgainOnFailover = 5,
                // release memory buffer after each 50 MB
                MaximumMemoryBufferBytes = 1024 * 1024 * 50,
                // download parts of the file as parallel or not. The default value is false
                ParallelDownload = true,
                // number of parallel downloads. The default value is the same as the chunk count
                //ParallelCount = 4,
                // timeout (millisecond) per stream block reader, default values is 1000
                Timeout = 1000,
                // set true if you want to download just a specific range of bytes of a large file
                RangeDownload = false,
                // floor offset of download range of a large file
                RangeLow = 0,
                // ceiling offset of download range of a large file
                RangeHigh = 0,
                // clear package chunks data when download completed with failure, default value is false
                ClearPackageOnCompletionWithFailure = true,
                // minimum size of chunking to download a file in multiple parts, the default value is 512
                MinimumSizeOfChunking = 1024,
                // Before starting the download, reserve the storage space of the file as file size, the default value is false
                ReserveStorageSpaceBeforeStartingDownload = true,
                // Get on demand downloaded data with ReceivedBytes on downloadProgressChanged event 
                EnableLiveStreaming = false,
                // config and customize request headers
                RequestConfiguration =
                {
        Accept = "*/*",
        CookieContainer = cookie,
        Headers = new WebHeaderCollection(), // { your custom headers }
        KeepAlive = true, // default value is false
        ProtocolVersion = HttpVersion.Version11, // default value is HTTP 1.1
        UseDefaultCredentials = false,
        // your custom user agent or your_app_name/app_version.
        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
        //Proxy = new WebProxy() {
        //   Address = new Uri("http://YourProxyServer/proxy.pac"),
        //   UseDefaultCredentials = false,
        //   Credentials = System.Net.CredentialCache.DefaultNetworkCredentials,
        //   BypassProxyOnLocal = true
        //}
    }
            };
            return downloadOpt;
        }
        public static double ByteToMB(long byteValue)
        {
            return byteValue / 1024.0 / 1024.0;
        }

        public static double ByteToKB(long byteValue)
        {
            return byteValue / 1024.0 ;
        }

        public static double ByteToGB(long byteValue)
        {
            return byteValue / 1024.0 / 1024.0 / 1024.0;
        }

        public static double FitByteUnit(long byteValue,out string unit)
        {
            double finalValue = 0.00;
            double kb = ByteToKB(byteValue);
            finalValue = kb;
            unit = "KB";
            if (kb > 1024)
            {
                double mb = ByteToMB(byteValue);
                finalValue = mb;
                unit = "MB";
                if (mb > 1024) 
                {
                    double gb = ByteToGB(byteValue);
                    finalValue = gb;
                    unit = "GB";
                }
            }
            return finalValue;

        }

        public static double FitByteUnit(long byteValue, string forceUnit)
        {
            double finalValue = 0;
            switch (forceUnit)
            {
                case "KB":
                    finalValue=ByteToKB(byteValue);
                    break;
                case "MB": 
                    finalValue = ByteToMB(byteValue); 
                    break;
                case "GB":
                    finalValue = ByteToGB(byteValue);
                    break;
            }
            return finalValue;

        }

    }
    

}
