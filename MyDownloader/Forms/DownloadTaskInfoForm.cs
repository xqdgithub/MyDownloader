using AntdUI;
using Downloader;
using MyDownloader.Classes;
using MyDownloader.Forms;

namespace MyDownloader
{
    public partial class DownloadTaskInfoForm : Form
    {
        private readonly string _taskId;
        private readonly System.Windows.Forms.Timer _timer;

        public DownloadTaskInfoForm(string taskId)
        {
            InitializeComponent();
            _taskId = taskId;
            _timer = new System.Windows.Forms.Timer { Interval = 500 };
            _timer.Tick += (_, _) => RefreshView();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            var snap = DownloadManager.Instance.GetSnapshot(_taskId);
            if (snap != null)
            {
                input1.Text = snap.Url ?? string.Empty;
                input2.Text = snap.FilePath;
            }
            RefreshView();
            _timer.Start();
        }

        private void RefreshView()
        {
            var snap = DownloadManager.Instance.GetSnapshot(_taskId);
            if (snap == null) return;

            progressTotal.Value = (float)(snap.Progress / 100.0);
            label2.Text = $"速度:{snap.SpeedText}";
            label3.Text = $"总览:{snap.ReceivedBytes}/{snap.TotalBytes}";
            label4.Text = snap.Status switch
            {
                DownloadStatus.Completed => "已完成",
                DownloadStatus.Failed => "失败:" + snap.ErrorMessage,
                DownloadStatus.Paused => "已暂停",
                DownloadStatus.Running => "正在下载",
                _ => "未开始"
            };
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            using var selectForm = new DownloadTaskSelectForm((input1.Text, input2.Text));
            if (selectForm.ShowDialog() == DialogResult.OK)
            {
                var res = selectForm.Result;
                DownloadManager.Instance.Remove(_taskId);
                DownloadManager.Instance.AddTask(res.url, res.path);
                Close();
            }
        }

        private void button1_Click_1(object sender, EventArgs e) => DownloadManager.Instance.Pause(_taskId);

        private void button2_Click(object sender, EventArgs e) => DownloadManager.Instance.Resume(_taskId);

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer?.Dispose();
            base.Dispose(disposing);
        }
    }
}
