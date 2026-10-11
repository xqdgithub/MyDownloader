using MyDownloader.Classes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MyDownloader.Forms
{
    public partial class DownloadTaskSelectForm : Form
    {
        public DownloadTaskSelectForm()
        {
            InitializeComponent();
        }

        public DownloadTaskSelectForm((string url, string path) source)
        {
            InitializeComponent();
            inputUrl.Text = source.url;
            if (!string.IsNullOrEmpty(source.path))
            {
                searchInput1.InputControl.Text = source.path;
            }
            else
            {
                // 传入空路径时（如 AddFromUrl），显式填充"默认目录+推导文件名"，
                // 不依赖 TextChanged 的触发时机，保证用户看到默认目录。
                inputUrl_TextChanged(inputUrl, EventArgs.Empty);
            }
        }

        public (string url, string path) Result { get; set; }


        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void DownloadTaskSelectForm_Load(object sender, EventArgs e)
        {
            searchInput1.ButtonControl.Click += SearchClick;
        }

        private void SearchClick(object? sender, EventArgs e)
        {
            using var saveFileDialog = new SaveFileDialog();
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                searchInput1.InputControl.Text = saveFileDialog.FileName;
            }

        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            Result = (inputUrl.Text, searchInput1.InputControl.Text);
            DialogResult = DialogResult.OK;
        }

        private void inputUrl_TextChanged(object sender, EventArgs e)
        {
            var dir = DownloadManager.Instance.Settings.GetDefaultDir();
            var file = PathTools.BuildFileName(inputUrl.Text, DateTime.Now);
            searchInput1.InputControl.Text = Path.Combine(dir, file);
        }
    }
}
