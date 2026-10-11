using Avalonia.Controls;
using Avalonia.Input;
using MyDownloader.App.Services;
using MyDownloader.App.ViewModels;

namespace MyDownloader.App.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            _vm.RequestAddTask = ShowAddTaskAsync;
            _vm.RequestShowDetail = ShowDetail;
            DataContext = _vm;

            var theme = this.FindControl<Button>("ThemeButton");
            if (theme != null) theme.Click += (_, _) => ThemeService.Toggle();

            var add = this.FindControl<Button>("AddButton");
            if (add != null) add.Click += (_, _) => _ = AddFromDialogAsync(null);

            AddHandler(DragDrop.DropEvent, OnDrop);
            AddHandler(DragDrop.DragOverEvent, OnDragOver);
        }

        public void PromptAddUrl(string url) => _ = AddFromDialogAsync(url);

        /// <summary>弹添加对话框，确认后把任务交给引擎。</summary>
        private async Task AddFromDialogAsync(string? url)
        {
            var result = await ShowAddTaskAsync(url);
            if (result is { } r) _vm.AddTask(r.url, r.path);
        }

        private async Task<(string url, string path)?> ShowAddTaskAsync(string? url)
        {
            var dlg = new AddTaskWindow(new AddTaskViewModel(url ?? string.Empty));
            var ok = await dlg.ShowDialog<bool>(this);
            return ok ? (dlg.UrlResult, dlg.PathResult) : null;
        }

        private void ShowDetail(string id)
        {
            var win = new TaskDetailWindow(id);
            win.Show(this);
        }

        private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (_vm.SelectedItem != null) ShowDetail(_vm.SelectedItem.Id);
        }

        private void OnDragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = e.DataTransfer.Contains(DataFormat.Text) || e.DataTransfer.Contains(DataFormat.File)
                ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnDrop(object? sender, DragEventArgs e)
        {
            var text = e.DataTransfer.TryGetText();
            if (!string.IsNullOrWhiteSpace(text) && text.Trim().StartsWith("http"))
            {
                _ = AddFromDialogAsync(text.Trim());
            }
        }
    }
}
