using Avalonia.Controls;
using MyDownloader.App.ViewModels;

namespace MyDownloader.App.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
