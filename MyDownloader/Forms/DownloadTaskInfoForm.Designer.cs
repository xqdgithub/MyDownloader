namespace MyDownloader
{
    partial class DownloadTaskInfoForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            btnStart = new AntdUI.Button();
            progressTotal = new AntdUI.Progress();
            label1 = new AntdUI.Label();
            tableLayoutPanel1 = new TableLayoutPanel();
            flowLayoutPanel1 = new FlowLayoutPanel();
            label2 = new AntdUI.Label();
            label3 = new AntdUI.Label();
            label4 = new AntdUI.Label();
            flowLayoutPanel2 = new FlowLayoutPanel();
            flowLayoutPanel4 = new FlowLayoutPanel();
            label5 = new AntdUI.Label();
            input2 = new AntdUI.Input();
            flowLayoutPanel5 = new FlowLayoutPanel();
            label6 = new AntdUI.Label();
            input1 = new AntdUI.Input();
            divider1 = new AntdUI.Divider();
            flowLayoutPanel3 = new FlowLayoutPanel();
            button1 = new AntdUI.Button();
            button2 = new AntdUI.Button();
            tableLayoutPanel1.SuspendLayout();
            flowLayoutPanel1.SuspendLayout();
            flowLayoutPanel4.SuspendLayout();
            flowLayoutPanel5.SuspendLayout();
            flowLayoutPanel3.SuspendLayout();
            SuspendLayout();
            // 
            // btnStart
            // 
            btnStart.Location = new Point(3, 108);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(100, 44);
            btnStart.TabIndex = 0;
            btnStart.Text = "任务编辑";
            btnStart.Type = AntdUI.TTypeMini.Warn;
            btnStart.Click += btnStart_Click;
            // 
            // progressTotal
            // 
            progressTotal.BackColor = Color.White;
            tableLayoutPanel1.SetColumnSpan(progressTotal, 2);
            progressTotal.Dock = DockStyle.Fill;
            progressTotal.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 134);
            progressTotal.IconRatio = 0.1F;
            progressTotal.LoadingFull = true;
            progressTotal.Location = new Point(10, 183);
            progressTotal.Margin = new Padding(10, 3, 10, 3);
            progressTotal.Name = "progressTotal";
            progressTotal.Shape = AntdUI.TShape.Default;
            progressTotal.ShowText = true;
            progressTotal.Size = new Size(818, 19);
            progressTotal.TabIndex = 1;
            progressTotal.Text = "%";
            // 
            // label1
            // 
            label1.Location = new Point(34, 171);
            label1.Name = "label1";
            label1.Size = new Size(149, 29);
            label1.TabIndex = 2;
            label1.Text = "";
            label1.TooltipConfig = null;
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.BackColor = Color.White;
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.6491652F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 87.35084F));
            tableLayoutPanel1.Controls.Add(flowLayoutPanel1, 0, 8);
            tableLayoutPanel1.Controls.Add(flowLayoutPanel2, 0, 6);
            tableLayoutPanel1.Controls.Add(flowLayoutPanel4, 0, 2);
            tableLayoutPanel1.Controls.Add(flowLayoutPanel5, 0, 1);
            tableLayoutPanel1.Controls.Add(divider1, 0, 4);
            tableLayoutPanel1.Controls.Add(flowLayoutPanel3, 1, 3);
            tableLayoutPanel1.Controls.Add(btnStart, 0, 3);
            tableLayoutPanel1.Controls.Add(progressTotal, 0, 5);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 9;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 5F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50.78125F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 49.21875F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            tableLayoutPanel1.Size = new Size(838, 581);
            tableLayoutPanel1.TabIndex = 4;
            // 
            // flowLayoutPanel1
            // 
            tableLayoutPanel1.SetColumnSpan(flowLayoutPanel1, 2);
            flowLayoutPanel1.Controls.Add(label2);
            flowLayoutPanel1.Controls.Add(label3);
            flowLayoutPanel1.Controls.Add(label4);
            flowLayoutPanel1.Dock = DockStyle.Bottom;
            flowLayoutPanel1.Location = new Point(3, 547);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new Size(832, 31);
            flowLayoutPanel1.TabIndex = 4;
            // 
            // label2
            // 
            label2.AutoSizeMode = AntdUI.TAutoSize.Width;
            label2.Dock = DockStyle.Fill;
            label2.Location = new Point(3, 3);
            label2.Name = "label2";
            label2.Size = new Size(0, 21);
            label2.TabIndex = 0;
            label2.Text = "";
            label2.TextMultiLine = false;
            label2.TooltipConfig = null;
            // 
            // label3
            // 
            label3.AutoSizeMode = AntdUI.TAutoSize.Width;
            label3.Dock = DockStyle.Fill;
            label3.Location = new Point(9, 3);
            label3.Name = "label3";
            label3.Size = new Size(0, 21);
            label3.TabIndex = 1;
            label3.Text = "";
            label3.TextMultiLine = false;
            label3.TooltipConfig = null;
            // 
            // label4
            // 
            label4.AutoEllipsis = true;
            label4.Dock = DockStyle.Fill;
            label4.Location = new Point(15, 3);
            label4.Name = "label4";
            label4.Size = new Size(94, 21);
            label4.TabIndex = 2;
            label4.Text = "";
            label4.TooltipConfig = null;
            // 
            // flowLayoutPanel2
            // 
            flowLayoutPanel2.AutoSize = true;
            flowLayoutPanel2.BackColor = Color.White;
            tableLayoutPanel1.SetColumnSpan(flowLayoutPanel2, 2);
            flowLayoutPanel2.Dock = DockStyle.Fill;
            flowLayoutPanel2.FlowDirection = FlowDirection.TopDown;
            flowLayoutPanel2.Location = new Point(3, 208);
            flowLayoutPanel2.Name = "flowLayoutPanel2";
            flowLayoutPanel2.Padding = new Padding(0, 0, 5, 0);
            tableLayoutPanel1.SetRowSpan(flowLayoutPanel2, 2);
            flowLayoutPanel2.Size = new Size(832, 333);
            flowLayoutPanel2.TabIndex = 5;
            // 
            // flowLayoutPanel4
            // 
            tableLayoutPanel1.SetColumnSpan(flowLayoutPanel4, 2);
            flowLayoutPanel4.Controls.Add(label5);
            flowLayoutPanel4.Controls.Add(input2);
            flowLayoutPanel4.Dock = DockStyle.Fill;
            flowLayoutPanel4.Location = new Point(0, 55);
            flowLayoutPanel4.Margin = new Padding(0);
            flowLayoutPanel4.Name = "flowLayoutPanel4";
            flowLayoutPanel4.Size = new Size(838, 50);
            flowLayoutPanel4.TabIndex = 7;
            flowLayoutPanel4.Paint += flowLayoutPanel4_Paint;
            // 
            // label5
            // 
            label5.Anchor = AnchorStyles.Left;
            label5.AutoSizeMode = AntdUI.TAutoSize.Auto;
            label5.Location = new Point(3, 14);
            label5.Margin = new Padding(3, 3, 0, 3);
            label5.Name = "label5";
            label5.Size = new Size(80, 21);
            label5.TabIndex = 0;
            label5.Text = "  保存路径:";
            label5.TooltipConfig = null;
            label5.Click += label5_Click;
            // 
            // input2
            // 
            input2.Location = new Point(84, 1);
            input2.Margin = new Padding(1);
            input2.Name = "input2";
            input2.PlaceholderText = "--";
            input2.Radius = 10;
            input2.Size = new Size(744, 48);
            input2.TabIndex = 1;
            input2.WaveSize = 2;
            // 
            // flowLayoutPanel5
            // 
            tableLayoutPanel1.SetColumnSpan(flowLayoutPanel5, 2);
            flowLayoutPanel5.Controls.Add(label6);
            flowLayoutPanel5.Controls.Add(input1);
            flowLayoutPanel5.Dock = DockStyle.Fill;
            flowLayoutPanel5.Location = new Point(0, 5);
            flowLayoutPanel5.Margin = new Padding(0);
            flowLayoutPanel5.Name = "flowLayoutPanel5";
            flowLayoutPanel5.Size = new Size(838, 50);
            flowLayoutPanel5.TabIndex = 8;
            // 
            // label6
            // 
            label6.Anchor = AnchorStyles.Left;
            label6.AutoSizeMode = AntdUI.TAutoSize.Auto;
            label6.Location = new Point(3, 15);
            label6.Margin = new Padding(3, 3, 0, 3);
            label6.Name = "label6";
            label6.Size = new Size(80, 21);
            label6.TabIndex = 0;
            label6.Text = "  下载地址:";
            label6.TooltipConfig = null;
            // 
            // input1
            // 
            input1.Location = new Point(84, 1);
            input1.Margin = new Padding(1);
            input1.Name = "input1";
            input1.PlaceholderText = "--";
            input1.Radius = 10;
            input1.Size = new Size(744, 49);
            input1.TabIndex = 1;
            input1.WaveSize = 2;
            // 
            // divider1
            // 
            divider1.ColorSplit = SystemColors.ControlDark;
            tableLayoutPanel1.SetColumnSpan(divider1, 2);
            divider1.Dock = DockStyle.Fill;
            divider1.Font = new Font("Microsoft YaHei UI", 7.8F, FontStyle.Regular, GraphicsUnit.Point, 0);
            divider1.ForeColor = SystemColors.ActiveCaptionText;
            divider1.Location = new Point(3, 158);
            divider1.Name = "divider1";
            divider1.Size = new Size(832, 19);
            divider1.TabIndex = 9;
            divider1.Text = "进度";
            divider1.TextPadding = 0F;
            divider1.Thickness = 1F;
            // 
            // flowLayoutPanel3
            // 
            flowLayoutPanel3.Controls.Add(button1);
            flowLayoutPanel3.Controls.Add(button2);
            flowLayoutPanel3.Location = new Point(106, 105);
            flowLayoutPanel3.Margin = new Padding(0);
            flowLayoutPanel3.Name = "flowLayoutPanel3";
            flowLayoutPanel3.Size = new Size(567, 50);
            flowLayoutPanel3.TabIndex = 6;
            // 
            // button1
            // 
            button1.Location = new Point(3, 3);
            button1.Name = "button1";
            button1.Size = new Size(78, 44);
            button1.TabIndex = 1;
            button1.Text = "暂停";
            button1.Type = AntdUI.TTypeMini.Primary;
            button1.Click += button1_Click_1;
            // 
            // button2
            // 
            button2.Location = new Point(87, 3);
            button2.Name = "button2";
            button2.Size = new Size(72, 44);
            button2.TabIndex = 2;
            button2.Text = "继续";
            button2.Type = AntdUI.TTypeMini.Primary;
            button2.Click += button2_Click;
            // 
            // DownloadTaskInfoForm
            // 
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(838, 581);
            Controls.Add(tableLayoutPanel1);
            Controls.Add(label1);
            DoubleBuffered = true;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Name = "DownloadTaskInfoForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "下载任务";
            Load += MainForm_Load;
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            flowLayoutPanel1.ResumeLayout(false);
            flowLayoutPanel1.PerformLayout();
            flowLayoutPanel4.ResumeLayout(false);
            flowLayoutPanel4.PerformLayout();
            flowLayoutPanel5.ResumeLayout(false);
            flowLayoutPanel5.PerformLayout();
            flowLayoutPanel3.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private AntdUI.Button btnStart;
        private AntdUI.Progress progressTotal;
        private AntdUI.Label label1;
        private TableLayoutPanel tableLayoutPanel1;
        private FlowLayoutPanel flowLayoutPanel1;
        private AntdUI.Label label2;
        private AntdUI.Label label3;
        private AntdUI.Label label4;
        private FlowLayoutPanel flowLayoutPanel2;
        private FlowLayoutPanel flowLayoutPanel3;
        private AntdUI.Button button1;
        private AntdUI.Button button2;
        private FlowLayoutPanel flowLayoutPanel4;
        private AntdUI.Label label5;
        private FlowLayoutPanel flowLayoutPanel5;
        private AntdUI.Label label6;
        private AntdUI.Divider divider1;
        private AntdUI.Input input2;
        private AntdUI.Input input1;
    }
}
