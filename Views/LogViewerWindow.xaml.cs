using GeminiGUI.ViewModels;
using System.Windows;

namespace GeminiGUI.Views
{
    public partial class LogViewerWindow : Window
    {
        public SettingsViewModel ViewModel { get; }

        public LogViewerWindow(SettingsViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}


