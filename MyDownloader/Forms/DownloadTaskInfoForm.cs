using AntdUI;
using Downloader;
using MyDownloader.Classes;
using MyDownloader.Forms;
using QDX.Serivices.StorageServices;
using QDX.Serivices.StorageServices.SQLiteDataProvider;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;

namespace MyDownloader
{
    public partial class DownloadTaskInfoForm : Form
    {
        IDataProvider dataProvider = new SqLiteDataProvider()
        {
            SqliteDataBase = ConstData.DownloadMainDB
        };

        DownloadTaskInfo? downloadTaskInfo;

        private DownloadService downloader;

        private static ConcurrentDictionary<string, Progress> ChildConsoleProgresses = new ConcurrentDictionary<string, Progress>();
        public DownloadTaskInfoForm()
        {
            InitializeComponent();
            downloadTaskInfo = null;
        }

        public DownloadTaskInfoForm(DownloadTaskInfo taskInfo)
        {
            InitializeComponent();
            downloadTaskInfo = taskInfo;
        }

        /// <summary>按任务 Id 构造（Task 11 将完整重写本窗口，此处仅保证编译）。</summary>
        public DownloadTaskInfoForm(string taskId)
        {
            InitializeComponent();
            downloadTaskInfo = null;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Init();
            input1.Focus();
        }

        private void Init()
        {
            if (downloadTaskInfo != null)
            {
                input1.Text = downloadTaskInfo.Url;
                input2.Text = downloadTaskInfo.FilePath;
                if(downloadTaskInfo.DownloadPackage.SaveProgress>0)
                progressTotal.Value = (float)downloadTaskInfo.DownloadPackage?.SaveProgress / 100;
            }
        }

        public async Task StartDownloadAsync()
        {
            if (!this.IsDisposed)
            {
               await StartDownload(downloadTaskInfo?.Url, downloadTaskInfo?.FilePath);
            }
            
        }

        private async void btnStart_Click(object sender, EventArgs e)
        {
            using DownloadTaskSelectForm downloadTaskSelectForm = new DownloadTaskSelectForm((input1.Text, input2.Text));
            if (downloadTaskSelectForm.ShowDialog() == DialogResult.OK)
            {
                
                var res = downloadTaskSelectForm.Result;
                input2.Text = $"{res.path} ";
                input1.Text = $"{res.url} ";
               await StartDownload(res.url, res.path);
            }

        }

        private void AddOrUpdateTaskInfoAsync()
        {
            var downloadTaskInfos = dataProvider.Get<List<DownloadTaskInfo>>(ConstData.DownloadTaskInfoList)??new List<DownloadTaskInfo>();
            var info= downloadTaskInfos.FirstOrDefault(x=>x.ID==(downloadTaskInfo?.ID??"-"));

            if (info == null)
            {
                downloadTaskInfos.Add(downloadTaskInfo);
            }
            else
            {
                info=downloadTaskInfo;
            }
            dataProvider.Set(ConstData.DownloadTaskInfoList, downloadTaskInfos);
        }

        private async Task StartDownload(string url, string filePath)
        {
            if(string.IsNullOrEmpty(url) || string.IsNullOrEmpty(filePath))
            {
                return;
            }
            try
            {
                var option = DownloadTools.GetDefaultDowmloadOptions(null);

                flowLayoutPanel2.Height = option.ChunkCount * 25 + 10;

                downloader = new DownloadService(option);

                if (downloadTaskInfo == null)
                {
                    downloadTaskInfo = new DownloadTaskInfo()
                    {
                        Url = url,
                        FilePath = filePath,
                        DownloadPackage = downloader.Package,
                        StartTime = DateTime.Now,
                        EndTime = DateTime.MinValue,
                    };

                }
                else
                {
                    downloadTaskInfo.Url = url;
                    downloadTaskInfo.FilePath = filePath;
                    downloadTaskInfo.DownloadPackage= downloader.Package;
                }
                // Provide `FileName` and `TotalBytesToReceive` at the start of each download
                downloader.DownloadStarted += OnDownloadStarted;
                // Provide any information about chunker downloads, 
                // like progress percentage per chunk, speed, 
                // total received bytes and received bytes array to live streaming.
                downloader.ChunkDownloadProgressChanged += OnChunkDownloadProgressChanged;
                // Provide any information about download progress, 
                // like progress percentage of sum of chunks, total speed, 
                // average speed, total received bytes and received bytes array 
                // to live streaming.
                downloader.DownloadProgressChanged += OnDownloadProgressChanged;
                // Download completed event that can include errors or 
                // canceled or download completed successfully.
                downloader.DownloadFileCompleted += OnDownloadFileCompleted;
                await downloader.DownloadFileTaskAsync(url, filePath);
            }
            finally
            {
                AddOrUpdateTaskInfoAsync();
            }
            
        }

        private void OnDownloadFileCompleted(object? sender, AsyncCompletedEventArgs e)
        {
        }

        private void OnDownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
        {
            updateTotalPercentAsync(e);
        }

        private void OnChunkDownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
        {
            this.BeginInvoke(new Action(() => { RefreshProgress(e); }));
        }

        private void OnDownloadStarted(object? sender, DownloadStartedEventArgs e)
        {
        }

        private void updateTotalPercentAsync(DownloadProgressChangedEventArgs e)
        {
            this.BeginInvoke(new Action(() =>
            {
                progressTotal.Value = (float)e.ProgressPercentage / 100;
                if (progressTotal.Value >= 1)
                {
                    progressTotal.State = AntdUI.TType.Success;
                    label4.Text = "已完成";
                    return;
                }
                var speed = DownloadTools.FitByteUnit((long)e.AverageBytesPerSecondSpeed, out string speedUnit);
                var received = DownloadTools.FitByteUnit(e.ReceivedBytesSize, out string receivedUnit);
                var total = DownloadTools.FitByteUnit(e.TotalBytesToReceive, out string totalUnit);
                label2.Text = $"速度:{speed.ToString("#0.00")}{speedUnit}/s";
                label3.Text = $"总览:{received.ToString("#0.00")}{receivedUnit}/{total.ToString("#0.00")}{totalUnit}";
                label4.Text = "正在下载";
            }));
        }
        private void RefreshSubProgrssControl()
        {
            if (downloader == null)
            {
                return;
            }


        }

        private void RefreshProgress(DownloadProgressChangedEventArgs args)
        {
            Progress progress = ChildConsoleProgresses.GetOrAdd(args.ProgressId, id =>
            {
                var prog = new Progress();
                prog.Name = $"progress{args.ProgressId}";
                prog.Shape = TShape.Round;
                prog.Fill = Color.LightSeaGreen;
                prog.Dock = DockStyle.Left;
                prog.Height = 25;
                prog.Width = flowLayoutPanel2.Width-80;
                prog.ShowText = true;
                prog.Text = "%";
                prog.Margin = new Padding(40, 3, 10, 3);
                flowLayoutPanel2.Controls.Add(prog);
                return prog;
            });
            progress.Value = (float)args.ProgressPercentage / 100;
            if (args.ProgressPercentage >= 100)
            {
                progress.State = AntdUI.TType.Success;
            }
            else
            {
                progress.State = AntdUI.TType.None;
            }
            //var speed = DownloadTools.FitByteUnit((long)args.AverageBytesPerSecondSpeed, out string speedUnit);
            //progress.Text = $"% 速度:{speed.ToString("#0.00")}{speedUnit}/s";
        }

        private void button1_Click(object sender, EventArgs e)
        {
            downloader.Pause();
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            downloader.Pause();
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            if (downloader == null)
            {
                var option = DownloadTools.GetDefaultDowmloadOptions(null);
                downloader = new DownloadService(option);
                downloader.DownloadStarted += OnDownloadStarted;
                downloader.ChunkDownloadProgressChanged += OnChunkDownloadProgressChanged;
                downloader.DownloadProgressChanged += OnDownloadProgressChanged;
                downloader.DownloadFileCompleted += OnDownloadFileCompleted;
            }else if (downloader.Package == null)
            {
                await downloader.DownloadFileTaskAsync(downloadTaskInfo?.DownloadPackage);
            }
            else
            {
                downloader.Resume();
            }
            
            //downloader.DownloadFileTaskAsync(downloader.Package);
        }

        private void flowLayoutPanel4_Paint(object sender, PaintEventArgs e)
        {

        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        private void InitFromTaskInfo()
        {
            if (downloadTaskInfo == null) { return; }
            input1.Text = downloadTaskInfo.Url??"";
            input2.Text = downloadTaskInfo.FilePath ?? "";
        }
    }
}
