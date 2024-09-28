using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Downloader;


namespace MyDownloader.Classes
{
    public class DownloadTaskInfo
    {
        public string ID { get; set; } = Guid.NewGuid().ToString();
        public DownloadPackage? DownloadPackage { get; set; }= new DownloadPackage() {Status= DownloadStatus.None,SaveProgress=0 };

        public string? Url { get; set; }
        public string? FilePath { get; set; }
        public DateTime StartTime{ get; set; }=DateTime.Now;
        public DateTime EndTime { get; set; } = DateTime.MinValue;
    }
}
