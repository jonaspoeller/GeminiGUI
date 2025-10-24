using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;

namespace GeminiGUI.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = App.GetService<ViewModels.SettingsViewModel>();
            
            // PasswordBox Event Handler
            ApiPasswordBox.PasswordChanged += ApiPasswordBox_PasswordChanged;
            
            // TextBox Event Handler
            ApiKeyTextBox.TextChanged += ApiKeyTextBox_TextChanged;
            
            // Load existing API key into PasswordBox and refresh display
            if (DataContext is ViewModels.SettingsViewModel viewModel)
            {
                // Load API key and update UI
                LoadApiKeyAsync(viewModel);
            }
        }

        private async void LoadApiKeyAsync(ViewModels.SettingsViewModel viewModel)
        {
            try
            {
                // Refresh API key display
                await viewModel.RefreshApiKeyAsync();
                
                // Set initial values after the API key is loaded
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    ApiPasswordBox.Password = viewModel.ApiKey;
                    if (viewModel.IsApiKeyVisible)
                    {
                        ApiKeyTextBox.Text = viewModel.ApiKey;
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (System.Exception ex)
            {
                // Log error but don't crash
                System.Diagnostics.Debug.WriteLine($"Error loading API key: {ex.Message}");
            }
        }

        private void ApiPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.SettingsViewModel viewModel)
            {
                viewModel.ApiKey = ApiPasswordBox.Password;
            }
        }

        private void ApiKeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is ViewModels.SettingsViewModel viewModel)
            {
                viewModel.ApiKey = ApiKeyTextBox.Text;
            }
        }

        private void ToggleVisibility_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.SettingsViewModel viewModel)
            {
                // Toggle the visibility
                viewModel.IsApiKeyVisible = !viewModel.IsApiKeyVisible;
                
                if (viewModel.IsApiKeyVisible)
                {
                    // Switching to visible - show TextBox, hide PasswordBox
                    ApiKeyTextBox.Visibility = System.Windows.Visibility.Visible;
                    ApiPasswordBox.Visibility = System.Windows.Visibility.Collapsed;
                    ToggleVisibilityButton.Content = "Hide";
                    
                    // Copy from PasswordBox to TextBox
                    ApiKeyTextBox.Text = ApiPasswordBox.Password;
                }
                else
                {
                    // Switching to hidden - show PasswordBox, hide TextBox
                    ApiKeyTextBox.Visibility = System.Windows.Visibility.Collapsed;
                    ApiPasswordBox.Visibility = System.Windows.Visibility.Visible;
                    ToggleVisibilityButton.Content = "Show";
                    
                    // Copy from TextBox to PasswordBox
                    ApiPasswordBox.Password = ApiKeyTextBox.Text;
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenApiKeyWebsite_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open the Google AI Studio API Keys page in the default browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://aistudio.google.com/api-keys",
                    UseShellExecute = true
                });
            }
            catch (System.Exception ex)
            {
                // Show error message if opening browser fails
                MessageBox.Show($"Error opening website: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
        }
    }
}
