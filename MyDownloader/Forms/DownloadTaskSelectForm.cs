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
            searchInput1.InputControl.Text=source.path;
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
            SaveFileDialog saveFileDialog = new SaveFileDialog();
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
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var file=ExtractFileName(inputUrl.Text);
            searchInput1.InputControl.Text = Path.Combine(desktopPath, file);
        }

        string ExtractFileName(string url)
        {
            var split = url.Split('/');
            if (split.Length > 0)
            {
                return split[^1];
            }
            else
            {
                return url;
            }
        }
    }
}
