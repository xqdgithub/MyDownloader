using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MyDownloader.App.ViewModels;

namespace MyDownloader.App.Views
{
    public partial class AddTaskWindow : Window
    {
        private readonly AddTaskViewModel _vm;

        public AddTaskWindow() : this(new AddTaskViewModel()) { }

        public AddTaskWindow(AddTaskViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            _vm.RequestClose = ok => Close(ok);
            DataContext = _vm;

            var browse = this.FindControl<Button>("BrowseButton");
            if (browse != null) browse.Click += async (_, _) => await BrowseAsync();
        }

        public string UrlResult => _vm.Url;
        public string PathResult => _vm.FilePath;

        private async Task BrowseAsync()
        {
            var files = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "选择保存位置",
                SuggestedFileName = System.IO.Path.GetFileName(_vm.FilePath),
                DefaultExtension = System.IO.Path.GetExtension(_vm.FilePath).TrimStart('.')
            });
            if (files != null)
            {
                _vm.FilePath = files.Path.LocalPath;
            }
        }
    }
}
