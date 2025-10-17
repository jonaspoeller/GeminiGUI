using GeminiGUI.ViewModels;
using System;
using System.Threading.Tasks;
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
            
            // Event-Handler für automatisches Scrollen
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
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

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.CurrentChatViewModel))
            {
                // Wenn sich der aktuelle Chat ändert, nach unten scrollen
                if (ViewModel.CurrentChatViewModel != null)
                {
                    // Event-Handler für neue Nachrichten hinzufügen
                    ViewModel.CurrentChatViewModel.Messages.CollectionChanged += Messages_CollectionChanged;
                    
                    // Nach unten scrollen
                    Task.Delay(200).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            var scrollViewer = FindScrollViewer(this);
                            scrollViewer?.ScrollToEnd();
                        });
                    });
                }
            }
        }

        private void Messages_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Bei neuen Nachrichten nach unten scrollen
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                Task.Delay(100).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        var scrollViewer = FindScrollViewer(this);
                        scrollViewer?.ScrollToEnd();
                    });
                });
            }
        }

        private ScrollViewer? FindScrollViewer(DependencyObject parent)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer scrollViewer && scrollViewer.Name == "MessagesScrollViewer")
                {
                    return scrollViewer;
                }
                var result = FindScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        private void ChatTitleTextBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            StartTitleEditing(sender as System.Windows.Controls.TextBox);
        }

        private void EditTitleButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Find the TextBox in the visual tree
            var textBox = FindTextBoxInVisualTree();
            StartTitleEditing(textBox);
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
                // Store original color
                _originalForeground = textBox.Foreground;
                
                textBox.IsReadOnly = false;
                textBox.Cursor = System.Windows.Input.Cursors.IBeam;
                // Keep original black color
                textBox.Foreground = _originalForeground;
                textBox.CaretBrush = System.Windows.Media.Brushes.Black; // Schwarzer Cursor
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
                    $"Möchten Sie den Chat '{viewModel.SelectedChat.Title}' wirklich löschen?",
                    "Chat löschen",
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