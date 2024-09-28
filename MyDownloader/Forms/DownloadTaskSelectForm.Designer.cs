namespace MyDownloader.Forms
{
    partial class DownloadTaskSelectForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            labelUrl = new AntdUI.Label();
            inputUrl = new AntdUI.Input();
            labelPath = new AntdUI.Label();
            btnOk = new AntdUI.Button();
            searchInput1 = new UserControls.SearchInput();
            SuspendLayout();
            // 
            // labelUrl
            // 
            labelUrl.Location = new Point(22, 37);
            labelUrl.Name = "labelUrl";
            labelUrl.Size = new Size(94, 26);
            labelUrl.TabIndex = 0;
            labelUrl.Text = "下载地址";
            labelUrl.TooltipConfig = null;
            // 
            // inputUrl
            // 
            inputUrl.Location = new Point(86, 26);
            inputUrl.Name = "inputUrl";
            inputUrl.Size = new Size(593, 51);
            inputUrl.TabIndex = 1;
            inputUrl.TextChanged += inputUrl_TextChanged;
            // 
            // labelPath
            // 
            labelPath.Location = new Point(22, 119);
            labelPath.Name = "labelPath";
            labelPath.Size = new Size(83, 26);
            labelPath.TabIndex = 3;
            labelPath.Text = "保存路径";
            labelPath.TooltipConfig = null;
            labelPath.Click += label2_Click;
            // 
            // btnOk
            // 
            btnOk.Location = new Point(562, 179);
            btnOk.Name = "btnOk";
            btnOk.Size = new Size(106, 42);
            btnOk.TabIndex = 4;
            btnOk.Text = "确定";
            btnOk.Type = AntdUI.TTypeMini.Primary;
            btnOk.Click += btnOk_Click;
            // 
            // searchInput1
            // 
            searchInput1.Location = new Point(86, 102);
            searchInput1.Name = "searchInput1";
            searchInput1.Size = new Size(593, 57);
            searchInput1.TabIndex = 5;
            // 
            // DownloadTaskSelectForm
            // 
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(691, 233);
            Controls.Add(searchInput1);
            Controls.Add(btnOk);
            Controls.Add(labelPath);
            Controls.Add(inputUrl);
            Controls.Add(labelUrl);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "DownloadTaskSelectForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "新建任务";
            TopMost = true;
            Load += DownloadTaskSelectForm_Load;
            ResumeLayout(false);
        }

        #endregion

        private AntdUI.Label labelUrl;
        private AntdUI.Input inputUrl;
        private AntdUI.Label labelPath;
        private AntdUI.Button btnOk;
        private UserControls.SearchInput searchInput1;
    }
}