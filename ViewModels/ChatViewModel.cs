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

        public event EventHandler<bool>? LoadingStateChanged;

        public ChatViewModel(IChatService chatService, IDatabaseService databaseService, Chat chat)
        {
            _chatService = chatService;
            _databaseService = databaseService;
            _chat = chat;
            _chatTitle = chat.Title;
            
            // Timer für animierte Punkte initialisieren
            _loadingAnimationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _loadingAnimationTimer.Tick += LoadingAnimationTimer_Tick;
            
            LoadMessagesAsync();
        }

        private void LoadingAnimationTimer_Tick(object sender, EventArgs e)
        {
            if (_disposed || _currentProcessingMessage == null) return;
            
            _loadingDotsCount = (_loadingDotsCount + 1) % 4; // 0, 1, 2, 3 für 0, 1, 2, 3 Punkte
            var dots = new string('.', _loadingDotsCount);
            
            // Aktualisiere die bestehende Nachricht statt ein neues Objekt zu erstellen
            _currentProcessingMessage.Content = $"Wird verarbeitet{dots}";
            OnPropertyChanged(nameof(Messages));
        }

        [RelayCommand]
        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentMessage) || IsLoading)
                return;

            // Schutz vor mehrfacher Ausführung
            if (IsLoading)
                return;

            var message = CurrentMessage.Trim();
            CurrentMessage = string.Empty;
            IsLoading = true;

            // 1. Datums-Trenner prüfen und ggf. hinzufügen
            var currentDate = DateTime.UtcNow.Date;
            var lastMessage = Messages.OfType<ChatMessage>().LastOrDefault();
            if (lastMessage == null || lastMessage.Timestamp.Date != currentDate)
            {
                Messages.Add(new DateSeparatorMessage(DateTime.UtcNow));
            }

            // 2. Benutzernachricht sofort anzeigen
            var userMessage = new ChatMessage
            {
                Role = "user",
                Content = message,
                Timestamp = DateTime.UtcNow
            };
            Messages.Add(userMessage);

            // 3. Sofort nach unten scrollen
            ScrollToBottom();

            // 4. "Wird verarbeitet..." Nachricht erstellen
            _currentProcessingMessage = new ChatMessage
            {
                Role = "model",
                Content = "Wird verarbeitet",
                Timestamp = DateTime.UtcNow
            };
            Messages.Add(_currentProcessingMessage);

            // 5. Nochmal nach unten scrollen
            ScrollToBottom();

            // 6. Animation starten
            _loadingAnimationTimer.Start();

            var fullResponse = "";

            try
            {
                // 5. Streaming von Gemini
                await foreach (var chunk in _chatService.SendMessageStreamAsync(_chat.Id, message))
                {
                    fullResponse += chunk;
                    
                    // Streaming-Nachricht live aktualisieren (mit Throttling)
                    if (_currentProcessingMessage != null)
                    {
                        var now = DateTime.UtcNow;
                        if ((now - _lastUIUpdate).TotalMilliseconds >= UIUpdateThrottleMs)
                        {
                            _currentProcessingMessage.Content = fullResponse;
                            OnPropertyChanged(nameof(Messages));
                            _lastUIUpdate = now;
                            
                            // Weniger häufiges Scrollen während Streaming
                            ScrollToBottom();
                        }
                    }
                }

                // 6. Finale UI-Aktualisierung sicherstellen
                if (_currentProcessingMessage != null)
                {
                    _currentProcessingMessage.Content = fullResponse;
                    OnPropertyChanged(nameof(Messages));
                }

                // 7. Beide Nachrichten in DB speichern
                await _databaseService.AddMessageAsync(_chat.Id, "user", message);
                await _databaseService.AddMessageAsync(_chat.Id, "model", fullResponse);
                await _databaseService.UpdateChatStatsAsync(_chat.Id);
            }
            catch (Exception ex)
            {
                // 7. Bei Fehlern: Streaming-Nachricht durch Fehlermeldung ersetzen
                if (_currentProcessingMessage != null)
                {
                    _currentProcessingMessage.Content = $"Fehler: {ex.Message}";
                    OnPropertyChanged(nameof(Messages));
                }
            }
            finally
            {
                // Animation stoppen
                _loadingAnimationTimer.Stop();
                _currentProcessingMessage = null;
                IsLoading = false;
            }
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
                // Fehlerbehandlung
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
            try
            {
                var messages = await _chatService.GetChatMessagesAsync(_chat.Id);
                
                // Temporär Event-Handler entfernen um mehrfache Scroll-Aufrufe zu vermeiden
                Messages.CollectionChanged -= OnMessagesCollectionChanged;
                
                Messages.Clear();
                
                DateTime? lastDate = null;
                foreach (var message in messages)
                {
                    // Datums-Trenner hinzufügen wenn sich das Datum ändert
                    if (lastDate == null || message.Timestamp.Date != lastDate.Value.Date)
                    {
                        Messages.Add(new DateSeparatorMessage(message.Timestamp));
                        lastDate = message.Timestamp.Date;
                    }
                    
                    Messages.Add(message);
                }
                
                // Event-Handler wieder hinzufügen
                Messages.CollectionChanged += OnMessagesCollectionChanged;
            }
            catch (Exception ex)
            {
                // Fehlerbehandlung
            }
        }

        private void OnMessagesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Bei neuen Nachrichten immer scrollen
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                // Scrollen wird jetzt im MainWindow gehandhabt
                // Hier nur ein Event auslösen
                OnPropertyChanged(nameof(Messages));
            }
        }

        private System.Windows.Controls.ScrollViewer? _cachedScrollViewer;
        private DateTime _lastScrollTime = DateTime.MinValue;
        private const int ScrollThrottleMs = 100; // Weniger häufiges Scrollen während Streaming
        private DateTime _lastUIUpdate = DateTime.MinValue;
        private const int UIUpdateThrottleMs = 30; // UI-Updates throttlen für bessere Performance

        private void ScrollToBottom()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastScrollTime).TotalMilliseconds < ScrollThrottleMs)
                return; // Skip if called too frequently
                
            _lastScrollTime = now;

            // Use cached scroll viewer if available
            if (_cachedScrollViewer != null)
            {
                _cachedScrollViewer.ScrollToEnd();
                return;
            }

            // Find scroll viewer only once and cache it
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var mainWindow = System.Windows.Application.Current.MainWindow as Views.MainWindow;
                if (mainWindow != null)
                {
                    _cachedScrollViewer = FindScrollViewer(mainWindow);
                    _cachedScrollViewer?.ScrollToEnd();
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private System.Windows.Controls.ScrollViewer? FindScrollViewer(System.Windows.DependencyObject parent)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Controls.ScrollViewer scrollViewer && scrollViewer.Name == "MessagesScrollViewer")
                {
                    return scrollViewer;
                }
                var result = FindScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _loadingAnimationTimer?.Stop();
                if (_loadingAnimationTimer != null)
                {
                    _loadingAnimationTimer.Tick -= LoadingAnimationTimer_Tick;
                    _loadingAnimationTimer = null;
                }
                _disposed = true;
            }
        }
    }
}