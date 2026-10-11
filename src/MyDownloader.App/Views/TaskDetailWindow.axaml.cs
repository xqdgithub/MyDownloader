using Avalonia.Controls;
using MyDownloader.App.ViewModels;

namespace MyDownloader.App.Views
{
    public partial class TaskDetailWindow : Window
    {
        private readonly TaskDetailViewModel _vm;

        public TaskDetailWindow() : this(string.Empty) { }

        public TaskDetailWindow(string taskId)
        {
            InitializeComponent();
            _vm = new TaskDetailViewModel(taskId);
            DataContext = _vm;
            Closed += (_, _) => _vm.Stop();
        }
    }
}
