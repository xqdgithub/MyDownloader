using AntdUI;
using MyDownloader.Classes;
using MyDownloader.Forms;
using QDX.Serivices.StorageServices;
using QDX.Serivices.StorageServices.SQLiteDataProvider;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MyDownloader
{
    public partial class MainForm : Form
    {
        IDataProvider dataProvider = new SqLiteDataProvider()
        {
            SqliteDataBase = ConstData.DownloadMainDB
        };
        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            InitMainTable();
        }

        private void InitMainTable()
        {
            tableMain.Columns = new AntdUI.ColumnCollection {
                new AntdUI.ColumnCheck("check"){ Fixed=false },
                new AntdUI.Column("fileName","文件名"){ Fixed=false,Width="auto"},
                new AntdUI.Column("size","大小"){ Fixed=false,Width="auto"},
                new AntdUI.Column("status","状态",AntdUI.ColumnAlign.Center),
                new AntdUI.Column("oper","操作",AntdUI.ColumnAlign.Center){ Fixed=false,Width="auto"},
            };
            var downloadTaskInfos = dataProvider.Get<List<DownloadTaskInfo>>(ConstData.DownloadTaskInfoList) ?? new List<DownloadTaskInfo>();
            List<TableItem> tableItems = new List<TableItem>();
            foreach (var item in downloadTaskInfos)
            {
                tableItems.Add(BuildTableItem(item));

            }
            tableMain.DataSource = tableItems;
        }
        public void RefreshMainTable()
        {
            var downloadTaskInfos = dataProvider.Get<List<DownloadTaskInfo>>(ConstData.DownloadTaskInfoList) ?? new List<DownloadTaskInfo>();
            List<TableItem> tableItems = new List<TableItem>();
            foreach (var item in downloadTaskInfos)
            {
                tableItems.Add(BuildTableItem(item));

            }
            tableMain.DataSource = tableItems;
        }


        private void tableMain_CellButtonClick(object sender, TableButtonEventArgs e)
        {
            if (e.Record is TableItem data)
            {
                switch (e.Btn.Id)
                {
                    case "view":
                        DownloadTaskInfoForm taskInfo = new DownloadTaskInfoForm(data.downloadTaskInfo);
                        taskInfo.Show();
                        break;
                    case "del":
                        var downloadTaskInfos = dataProvider.Get<List<DownloadTaskInfo>>(ConstData.DownloadTaskInfoList) ?? new List<DownloadTaskInfo>();
                        var item = downloadTaskInfos.FirstOrDefault(x => x.ID == data.Id||string.IsNullOrEmpty(x.ID));
                        if (item != null) {
                            downloadTaskInfos.Remove(item);
                            dataProvider.Set<List<DownloadTaskInfo>>(ConstData.DownloadTaskInfoList, downloadTaskInfos);
                            RefreshMainTable();
                        }
                        
                        break;
                }
            }
        }

        private TableItem BuildTableItem(DownloadTaskInfo taskInfo)
        {
            var total = taskInfo.DownloadPackage.TotalFileSize;
            var status = taskInfo.DownloadPackage.Status;
            TState state = TState.Default;
            switch (status)
            {
                case Downloader.DownloadStatus.Completed: state = TState.Success; break;
                case Downloader.DownloadStatus.Failed: state = TState.Error; break;
                case Downloader.DownloadStatus.None: state = TState.Default; break;
            }
            var totalStr = DownloadTools.FitByteUnit((long)total, out string unit);
            TableItem tableItem = new TableItem()
            {
                Id = taskInfo.ID,
                check = true,
                fileName = Path.GetFileName(taskInfo.FilePath),
                size = $"{totalStr}{unit}",
                status = new AntdUI.CellBadge(state),
                downloadTaskInfo = taskInfo,
            };
            return tableItem;
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            using DownloadTaskSelectForm downloadTaskSelectForm = new DownloadTaskSelectForm();
            if (downloadTaskSelectForm.ShowDialog() == DialogResult.OK)
            {

                var res = downloadTaskSelectForm.Result;
                DownloadTaskInfo downloadTaskInfo = new DownloadTaskInfo()
                {
                  ID=Guid.NewGuid().ToString(),
                  Url=res.url,
                  FilePath=res.path,
                };
                DownloadTaskInfoForm downloadTaskInfoForm = new DownloadTaskInfoForm(downloadTaskInfo);
                downloadTaskInfoForm.Show();
                await downloadTaskInfoForm.StartDownloadAsync();
                RefreshMainTable();
            }
        }
    }

    public class TableItem : NotifyProperty
    {
        public bool check { get; set; }

        public string? fileName { get; set; }

        public string? Id { get; set; }

        public string? size { get; set; }

        public CellBadge? status { get; set; }

        public CellLink[]? oper { get; set; } = new AntdUI.CellLink[] {
                        new AntdUI.CellLink("view","查看"),
                        new AntdUI.CellLink("del","删除")
                    };

        public DownloadTaskInfo? downloadTaskInfo { get; set; }
    }
}
