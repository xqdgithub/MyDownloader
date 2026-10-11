using AntdUI;
using MyDownloader.Classes;
using MyDownloader.Forms;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MyDownloader
{
    public partial class MainForm : Form
    {
        private readonly Dictionary<string, TableItem> _items = new();
        private bool _clipboardEnabled;
        private string _lastClipboard = string.Empty;

        private const int WM_CLIPBOARDUPDATE = 0x031D;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        public MainForm()
        {
            InitializeComponent();
            AllowDrop = true;
            DragEnter += MainForm_DragEnter;
            DragDrop += MainForm_DragDrop;
            FormClosing += MainForm_FormClosing;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            InitMainTable();
            ApplyClipboardSetting(DownloadManager.Instance.Settings.ClipboardMonitorEnabled);
            RefreshFromSnapshots(DownloadManager.Instance.GetSnapshots());
        }

        private void InitMainTable()
        {
            tableMain.Columns = new AntdUI.ColumnCollection {
                new AntdUI.ColumnCheck("check"){ Fixed=false },
                new AntdUI.Column("fileName","文件名"){ Fixed=false,Width="auto"},
                new AntdUI.Column("size","大小"){ Fixed=false,Width="auto"},
                new AntdUI.Column("progress","进度"){ Fixed=false,Width="auto"},
                new AntdUI.Column("speed","速度",AntdUI.ColumnAlign.Center),
                new AntdUI.Column("eta","剩余",AntdUI.ColumnAlign.Center),
                new AntdUI.Column("status","状态",AntdUI.ColumnAlign.Center),
                new AntdUI.Column("oper","操作",AntdUI.ColumnAlign.Center){ Fixed=false,Width="auto"},
            };
        }

        /// <summary>由 TrayAppContext 每 500ms 调用，按 Id 增量更新。</summary>
        public void RefreshFromSnapshots(IReadOnlyList<TaskSnapshot> snapshots)
        {
            if (IsDisposed) return;
            bool changed = false;

            foreach (var snap in snapshots)
            {
                if (!_items.TryGetValue(snap.Id, out var item))
                {
                    item = new TableItem { Id = snap.Id };
                    _items[snap.Id] = item;
                    changed = true;
                }
                if (item.Apply(snap)) changed = true;
            }

            var ids = snapshots.Select(s => s.Id).ToHashSet();
            foreach (var gone in _items.Keys.Where(k => !ids.Contains(k)).ToList())
            {
                _items.Remove(gone);
                changed = true;
            }

            if (changed)
            {
                tableMain.DataSource = _items.Values.ToList();
            }
        }

        private void tableMain_CellButtonClick(object sender, TableButtonEventArgs e)
        {
            if (e.Record is TableItem data)
            {
                switch (e.Btn.Id)
                {
                    case "view":
                        var form = new DownloadTaskInfoForm(data.Id);
                        form.Show();
                        break;
                    case "pause":
                        DownloadManager.Instance.Pause(data.Id);
                        break;
                    case "resume":
                        DownloadManager.Instance.Resume(data.Id);
                        break;
                    case "open":
                        var snap = DownloadManager.Instance.GetSnapshot(data.Id);
                        if (snap != null && File.Exists(snap.FilePath))
                            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{snap.FilePath}\"");
                        break;
                    case "del":
                        DownloadManager.Instance.Remove(data.Id);
                        _items.Remove(data.Id);
                        tableMain.DataSource = _items.Values.ToList();
                        break;
                }
            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            using var downloadTaskSelectForm = new DownloadTaskSelectForm();
            if (downloadTaskSelectForm.ShowDialog() == DialogResult.OK)
            {
                var res = downloadTaskSelectForm.Result;
                DownloadManager.Instance.AddTask(res.url, res.path);
                RefreshFromSnapshots(DownloadManager.Instance.GetSnapshots());
            }
            await Task.CompletedTask;
        }

        // ---- 拖拽 ----

        private void MainForm_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data == null) return;
            if (e.Data.GetDataPresent(DataFormats.Text)
                || e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void MainForm_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data == null) return;
            if (e.Data.GetDataPresent(DataFormats.FileDrop)
                && e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                foreach (var f in files.Where(x => x.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var line in File.ReadAllLines(f))
                    {
                        var url = line.Trim();
                        if (IsHttpUrl(url)) AddFromUrl(url);
                    }
                }
                return;
            }
            var text = e.Data.GetData(DataFormats.Text) as string;
            if (!string.IsNullOrWhiteSpace(text) && IsHttpUrl(text.Trim()))
                AddFromUrl(text.Trim());
        }

        private void AddFromUrl(string url)
        {
            using var selectForm = new DownloadTaskSelectForm((url, string.Empty));
            if (selectForm.ShowDialog() == DialogResult.OK)
            {
                var res = selectForm.Result;
                DownloadManager.Instance.AddTask(res.url, res.path);
            }
        }

        private static bool IsHttpUrl(string s)
            => s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        // ---- 剪贴板监听 ----

        public void ApplyClipboardSetting(bool enabled)
        {
            if (_clipboardEnabled == enabled) return;
            _clipboardEnabled = enabled;
            if (!IsHandleCreated) return;
            if (enabled) AddClipboardFormatListener(Handle);
            else RemoveClipboardFormatListener(Handle);
        }

        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            if (m.Msg == WM_CLIPBOARDUPDATE)
            {
                OnClipboardChanged();
            }
            base.WndProc(ref m);
        }

        private void OnClipboardChanged()
        {
            if (!_clipboardEnabled || !Clipboard.ContainsText()) return;
            var text = Clipboard.GetText().Trim();
            if (text == _lastClipboard || !IsHttpUrl(text)) return;
            _lastClipboard = text;

            if (Visible && WindowState != FormWindowState.Minimized)
            {
                AddFromUrl(text);
            }
            else
            {
                AppContext?.NotifyClipboardUrl(text);
            }
        }

        /// <summary>由托盘气泡点击触发：用指定 URL 打开添加框。</summary>
        public void PromptAddUrl(string url)
        {
            AddFromUrl(url);
        }

        // ---- 关闭即隐藏 ----

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !IsForcedClose)
            {
                e.Cancel = true;
                if (AppContext is TrayAppContext ctx) ctx.HideMainForm();
                else Hide();
            }
        }

        public bool IsForcedClose { get; private set; }

        /// <summary>所属托盘上下文（由 TrayAppContext 在创建后赋值）。</summary>
        public TrayAppContext? AppContext { get; set; }

        /// <summary>退出时由 TrayAppContext 调用，真正关闭。</summary>
        public void ForceClose()
        {
            IsForcedClose = true;
            Close();
        }
    }

    public class TableItem : NotifyProperty
    {
        public bool check { get; set; } = true;
        public string? fileName { get; set; }
        public string Id { get; set; } = string.Empty;
        public string? size { get; set; }
        public CellProgress? progress { get; set; }
        public string? speed { get; set; }
        public string? eta { get; set; }
        public CellBadge? status { get; set; }
        public CellLink[]? oper { get; set; }

        /// <summary>上一次的状态；用于判断 status/oper 是否真正变化，保持增量刷新。</summary>
        private Downloader.DownloadStatus? _lastStatus;

        /// <summary>把快照写回本行；有变化返回 true。</summary>
        public bool Apply(TaskSnapshot snap)
        {
            bool changed = false;
            var name = snap.FileName;
            if (fileName != name) { fileName = name; OnPropertyChanged(nameof(fileName)); changed = true; }

            var size = DownloadTools.FitByteUnit(snap.TotalBytes, out string unit);
            var sizeText = $"{size.ToString("#0.00")}{unit}";
            if (this.size != sizeText) { this.size = sizeText; OnPropertyChanged(nameof(this.size)); changed = true; }

            var pct = (float)(snap.Progress / 100.0);
            if (progress == null)
            {
                progress = new CellProgress(pct);
                changed = true;
            }
            else if (Math.Abs(progress.Value - pct) > 0.001f)
            {
                progress.Value = pct;
                OnPropertyChanged(nameof(progress));
                changed = true;
            }

            if (speed != snap.SpeedText) { speed = snap.SpeedText; OnPropertyChanged(nameof(speed)); changed = true; }
            if (eta != snap.Eta) { eta = snap.Eta; OnPropertyChanged(nameof(eta)); changed = true; }

            // 仅在状态真正变化时才重建 status/oper，避免每 500ms 触发全表刷新。
            if (_lastStatus != snap.Status)
            {
                _lastStatus = snap.Status;

                var state = snap.Status switch
                {
                    Downloader.DownloadStatus.Completed => TState.Success,
                    Downloader.DownloadStatus.Failed => TState.Error,
                    Downloader.DownloadStatus.Running => TState.Processing,
                    Downloader.DownloadStatus.Paused => TState.Warn,
                    _ => TState.Default
                };
                status = new CellBadge(state);
                OnPropertyChanged(nameof(status));

                oper = snap.Status switch
                {
                    Downloader.DownloadStatus.Completed => new[] { new CellLink("view", "查看"), new CellLink("open", "打开"), new CellLink("del", "删除") },
                    Downloader.DownloadStatus.Running => new[] { new CellLink("pause", "暂停"), new CellLink("view", "查看"), new CellLink("del", "删除") },
                    Downloader.DownloadStatus.Paused => new[] { new CellLink("resume", "继续"), new CellLink("view", "查看"), new CellLink("del", "删除") },
                    _ => new[] { new CellLink("resume", "开始"), new CellLink("view", "查看"), new CellLink("del", "删除") }
                };
                OnPropertyChanged(nameof(oper));

                changed = true;
            }
            return changed;
        }
    }
}
