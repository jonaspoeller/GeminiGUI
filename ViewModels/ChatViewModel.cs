using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GeminiGUI.Models;
using GeminiGUI.Services;
using System.Windows.Threading;
using System.Linq;

namespace GeminiGUI.ViewModels
{
    public partial class ChatViewModel : ObservableObject, IDisposable
    {
        private readonly IChatService _chatService;
        private readonly IDatabaseService _databaseService;
        private readonly Chat _chat;
        private readonly ILoggerService _logger;

        [ObservableProperty]
        private ObservableCollection<object> _messages = new();

        [ObservableProperty]
        private string _currentMessage = string.Empty;

        private bool _isLoading;
        
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    LoadingStateChanged?.Invoke(this, value);
                }
            }
        }

        [ObservableProperty]
        private string _chatTitle;

        [ObservableProperty]
        private bool _isEditingTitle;

        private DispatcherTimer _loadingAnimationTimer;
        private int _loadingDotsCount = 0;
        private ChatMessage? _currentProcessingMessage;
        private bool _disposed = false;
        private bool _isSending = false;

        public event EventHandler<bool>? LoadingStateChanged;

        public ChatViewModel(IChatService chatService, IDatabaseService databaseService, Chat chat, ILoggerService logger)
        {
            _chatService = chatService;
            _databaseService = databaseService;
            _chat = chat;
            _logger = logger;
            _chatTitle = chat.Title;
            
            // Initialize timer for animated dots
            _loadingAnimationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _loadingAnimationTimer.Tick += LoadingAnimationTimer_Tick;
            
            // Load messages asynchronously without blocking the UI thread
            _ = Task.Run(async () =>
            {
                await Task.Delay(1); // Allow constructor to complete
                await LoadMessagesAsync();
            });
        }

        private void LoadingAnimationTimer_Tick(object sender, EventArgs e)
        {
            if (_disposed || _currentProcessingMessage == null) return;
            
            _loadingDotsCount = (_loadingDotsCount + 1) % 4; // 0, 1, 2, 3 for 0, 1, 2, 3 dots
            var dots = new string('.', _loadingDotsCount);
            
            // Update the existing message instead of creating a new object
            _currentProcessingMessage.Content = $"Processing{dots}";
        }

        [RelayCommand]
        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentMessage) || _isSending)
                return;

            var message = CurrentMessage.Trim();
            CurrentMessage = string.Empty;
            _isSending = true;

            // 1. Check and add date separator if needed
            var currentDate = DateTime.UtcNow.Date;
            var lastMessage = Messages.OfType<ChatMessage>().LastOrDefault();
            if (lastMessage == null || lastMessage.Timestamp.Date != currentDate)
            {
                Messages.Add(new DateSeparatorMessage(DateTime.UtcNow));
            }

            // 2. Show user message immediately
            var userMessage = new ChatMessage
            {
                Role = "user",
                Content = message,
                Timestamp = DateTime.UtcNow
            };
            Messages.Add(userMessage);

            // 4. Create "Processing..." message
            _currentProcessingMessage = new ChatMessage
            {
                Role = "model",
                Content = "Processing...",
                Timestamp = DateTime.UtcNow
            };
            Messages.Add(_currentProcessingMessage);

            // 6. Start animation
            _loadingAnimationTimer.Start();

            // Yield to allow UI to update before starting background processing
            await Task.Yield();

            // Run the entire processing logic in a background thread - fire and forget
            _ = Task.Run(async () =>
            {
                var fullResponse = "";

                try
                {
                    // 5. Stream from Gemini
                    await foreach (var chunk in _chatService.SendMessageStreamAsync(_chat.Id, message).ConfigureAwait(false))
                    {
                        // Service already returns accumulated text, so just use it directly
                        fullResponse = chunk;
                        
                        // Update UI on UI thread - FAST now with async markdown rendering!
                        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (_currentProcessingMessage != null)
                            {
                                var now = DateTime.UtcNow;
                                if ((now - _lastUIUpdate).TotalMilliseconds >= UIUpdateThrottleMs)
                                {
                                    _currentProcessingMessage.Content = fullResponse;
                                    _lastUIUpdate = now;
                                }
                            }
                        }), System.Windows.Threading.DispatcherPriority.Normal);
                    }

                    // Stop animation timer FIRST before final update
                    _loadingAnimationTimer.Stop();

                    // Final UI update - FAST with async markdown rendering!
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (_currentProcessingMessage != null)
                        {
                            _currentProcessingMessage.Content = fullResponse;
                            _currentProcessingMessage = null;
                        }
                    }, System.Windows.Threading.DispatcherPriority.Normal);

                    // 7. Save both messages to database
                    await _databaseService.AddMessageAsync(_chat.Id, "user", message).ConfigureAwait(false);
                    await _databaseService.AddMessageAsync(_chat.Id, "model", fullResponse).ConfigureAwait(false);
                    await _databaseService.UpdateChatStatsAsync(_chat.Id).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error in SendMessageAsync: {ex.Message}", ex);
                    // On errors: Replace streaming message with error message
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (_currentProcessingMessage != null)
                        {
                            _currentProcessingMessage.Content = $"Error: {ex.Message}";
                        }
                    }, System.Windows.Threading.DispatcherPriority.Normal);
                }
                finally
                {
                    // Cleanup on UI thread
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _loadingAnimationTimer.Stop();
                        _currentProcessingMessage = null;
                        _isSending = false;
                    }, System.Windows.Threading.DispatcherPriority.Normal);
                }
            });
        }

        [RelayCommand]
        private void StartEditingTitle()
        {
            IsEditingTitle = true;
        }

        [RelayCommand]
        private async Task SaveTitleAsync()
        {
            if (string.IsNullOrWhiteSpace(ChatTitle))
                return;

            try
            {
                await _chatService.UpdateChatTitleAsync(_chat.Id, ChatTitle);
                IsEditingTitle = false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to save chat title: {ex.Message}", ex);
                ChatTitle = _chat.Title; // Revert to original title
                IsEditingTitle = false;
            }
        }

        [RelayCommand]
        private void CancelEditingTitle()
        {
            ChatTitle = _chat.Title;
            IsEditingTitle = false;
        }

        private async Task LoadMessagesAsync()
        {
            IsLoading = true;
            try
            {
                // Load messages on background thread
                var messages = await _chatService.GetChatMessagesAsync(_chat.Id).ConfigureAwait(false);
                
                // Update UI on UI thread using BeginInvoke (non-blocking)
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Temporarily remove event handler to avoid multiple scroll calls
                    Messages.CollectionChanged -= OnMessagesCollectionChanged;
                    
                    Messages.Clear();
                    
                    DateTime? lastDate = null;
                    foreach (var message in messages)
                    {
                        // Add date separator when date changes
                        if (lastDate == null || message.Timestamp.Date != lastDate.Value.Date)
                        {
                            Messages.Add(new DateSeparatorMessage(message.Timestamp));
                            lastDate = message.Timestamp.Date;
                        }
                        
                        Messages.Add(message);
                    }
                    
                    // Re-add event handler
                    Messages.CollectionChanged += OnMessagesCollectionChanged;
                    
                    IsLoading = false;
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception)
            {
                // Error handling on UI thread
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    IsLoading = false;
                }), System.Windows.Threading.DispatcherPriority.Normal);
            }
        }

        private DateTime _lastUIUpdate = DateTime.MinValue;
        private const int UIUpdateThrottleMs = 10; // Faster UI updates (was 30ms)

        private void OnMessagesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Trigger PropertyChanged for UI updates when new messages are added
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                OnPropertyChanged(nameof(Messages));
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Stop and cleanup timer
                _loadingAnimationTimer?.Stop();
                if (_loadingAnimationTimer != null)
                {
                    _loadingAnimationTimer.Tick -= LoadingAnimationTimer_Tick;
                    _loadingAnimationTimer = null;
                }
                
                // Remove event handlers to prevent memory leaks
                Messages.CollectionChanged -= OnMessagesCollectionChanged;
                
                // Clear references
                _currentProcessingMessage = null;
                
                _disposed = true;
            }
        }
    }
}