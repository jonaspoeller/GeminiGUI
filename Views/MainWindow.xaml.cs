using GeminiGUI.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeminiGUI.Views
{
    public partial class MainWindow : Window
    {
        public MainWindowViewModel ViewModel { get; }

        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;
        }

        private void ChatItem_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && item.DataContext is Models.Chat chat)
            {
                ViewModel.SelectedChat = chat;
            }
        }

        private void MessageInputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (ViewModel.CurrentChatViewModel != null && !ViewModel.CurrentChatViewModel.IsLoading)
                {
                    ViewModel.CurrentChatViewModel.SendMessageCommand.Execute(null);
                }
                e.Handled = true;
            }
        }

        private void ChatTitleTextBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            StartTitleEditing(sender as System.Windows.Controls.TextBox);
        }

        private System.Windows.Controls.TextBox? FindTextBoxInVisualTree()
        {
            return FindTextBoxInVisualTree(this);
        }

        private System.Windows.Controls.TextBox? FindTextBoxInVisualTree(System.Windows.DependencyObject parent)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Controls.TextBox textBox && textBox.Name == "ChatTitleTextBox")
                {
                    return textBox;
                }
                var result = FindTextBoxInVisualTree(child);
                if (result != null) return result;
            }
            return null;
        }

        private System.Windows.Media.Brush? _originalForeground;

        private void StartTitleEditing(System.Windows.Controls.TextBox? textBox)
        {
            if (textBox != null)
            {
                // Notify ViewModel
                if (DataContext is ViewModels.MainWindowViewModel viewModel)
                {
                    // No longer needed - button removed
                }

                // Store original color
                _originalForeground = textBox.Foreground;
                
                textBox.IsReadOnly = false;
                textBox.Cursor = System.Windows.Input.Cursors.IBeam;
                // Keep original black color
                textBox.Foreground = _originalForeground;
                textBox.CaretBrush = System.Windows.Media.Brushes.Black; // Black cursor
                textBox.Focus();
                // Don't select all text - just position cursor at end
                textBox.CaretIndex = textBox.Text.Length;
                
                // Add event handlers for exiting edit mode
                textBox.LostFocus += TextBox_LostFocus;
                textBox.KeyDown += TextBox_KeyDown;
                this.MouseDown += MainWindow_MouseDown;
            }
        }

        private void TextBox_LostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                ExitTitleEditing(textBox);
            }
        }

        private void TextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Escape)
                {
                    ExitTitleEditing(textBox);
                    e.Handled = true;
                }
            }
        }

        private void ExitTitleEditing(System.Windows.Controls.TextBox textBox)
        {
            // Notify ViewModel
            if (DataContext is ViewModels.MainWindowViewModel viewModel)
            {
                // No longer needed - button removed
            }

            textBox.IsReadOnly = true;
            textBox.Cursor = System.Windows.Input.Cursors.Arrow;
            // Reset to original color
            if (_originalForeground != null)
            {
                textBox.Foreground = _originalForeground;
            }
            // Reset cursor color to default
            textBox.CaretBrush = System.Windows.Media.Brushes.Black;
            
            // Remove focus to hide cursor and set focus to main window
            System.Windows.Input.Keyboard.ClearFocus();
            this.Focus();
            
            // Remove event handlers
            textBox.LostFocus -= TextBox_LostFocus;
            textBox.KeyDown -= TextBox_KeyDown;
            this.MouseDown -= MainWindow_MouseDown;
        }

        private void MainWindow_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Find the TextBox and exit editing if it's in edit mode
            var textBox = FindTextBoxInVisualTree();
            if (textBox != null && !textBox.IsReadOnly)
            {
                ExitTitleEditing(textBox);
            }
        }

        private void EditChatMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainWindowViewModel viewModel && viewModel.SelectedChat != null)
            {
                // Find the TextBox and start editing
                var textBox = FindTextBoxInVisualTree();
                if (textBox != null)
                {
                    StartTitleEditing(textBox);
                }
            }
        }

        private void DeleteChatMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainWindowViewModel viewModel && viewModel.SelectedChat != null)
            {
                // Show confirmation dialog
                var result = System.Windows.MessageBox.Show(
                    $"Do you really want to delete the chat '{viewModel.SelectedChat.Title}'?",
                    "Delete Chat",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    // Delete the chat
                    viewModel.DeleteChatCommand.Execute(viewModel.SelectedChat);
                }
            }
        }
    }
}