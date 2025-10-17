using GeminiGUI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace GeminiGUI.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsView()
        {
            InitializeComponent();
            ViewModel = App.GetService<SettingsViewModel>();
            DataContext = ViewModel;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Finde das MainWindow und schließe die Settings
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow?.ViewModel != null)
            {
                mainWindow.ViewModel.CloseSettingsCommand.Execute(null);
            }
        }
    }
}
