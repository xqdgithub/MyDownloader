using Avalonia.Controls;
using MyDownloader.App.Services;
using MyDownloader.App.ViewModels;

namespace MyDownloader.App.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();

            var theme = this.FindControl<Button>("ThemeButton");
            if (theme != null) theme.Click += (_, _) => ThemeService.Toggle();
        }
    }
}
