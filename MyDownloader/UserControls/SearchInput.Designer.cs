namespace MyDownloader.UserControls
{
    partial class SearchInput
    {
        /// <summary> 
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 组件设计器生成的代码

        /// <summary> 
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            tableLayoutPanel1 = new TableLayoutPanel();
            input1 = new AntdUI.Input();
            button1 = new AntdUI.Button();
            tableLayoutPanel1.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50F));
            tableLayoutPanel1.Controls.Add(input1, 0, 0);
            tableLayoutPanel1.Controls.Add(button1, 1, 0);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 1;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Size = new Size(318, 54);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // input1
            // 
            input1.Dock = DockStyle.Fill;
            input1.JoinRight = true;
            input1.Location = new Point(1, 1);
            input1.Margin = new Padding(1, 1, 0, 1);
            input1.Name = "input1";
            input1.Size = new Size(267, 52);
            input1.TabIndex = 0;
            // 
            // button1
            // 
            button1.Dock = DockStyle.Fill;
            button1.IconHoverSvg = "";
            button1.IconSvg = "SearchOutlined";
            button1.JoinLeft = true;
            button1.Location = new Point(268, 1);
            button1.Margin = new Padding(0, 1, 1, 1);
            button1.Name = "button1";
            button1.Size = new Size(49, 52);
            button1.TabIndex = 1;
            button1.Type = AntdUI.TTypeMini.Primary;
            // 
            // UserControl1
            // 
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(tableLayoutPanel1);
            Name = "UserControl1";
            Size = new Size(318, 54);
            tableLayoutPanel1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel tableLayoutPanel1;
        private AntdUI.Input input1;
        private AntdUI.Button button1;
    }
}
