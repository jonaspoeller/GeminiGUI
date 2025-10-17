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
    public partial class ChatViewModel : ObservableObject
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
            _loadingDotsCount = (_loadingDotsCount + 1) % 4; // 0, 1, 2, 3 für 0, 1, 2, 3 Punkte
            var dots = new string('.', _loadingDotsCount);
            
            // Finde die letzte "Wird verarbeitet..." Nachricht und aktualisiere sie
            for (int i = Messages.Count - 1; i >= 0; i--)
            {
                if (Messages[i] is ChatMessage message && message.Role == "model" && message.Content.StartsWith("Wird verarbeitet"))
                {
                    Messages[i] = new ChatMessage
                    {
                        Role = "model",
                        Content = $"Wird verarbeitet{dots}",
                        Timestamp = message.Timestamp
                    };
                    break;
                }
            }
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
            var processingMessage = new ChatMessage
            {
                Role = "model",
                Content = "Wird verarbeitet",
                Timestamp = DateTime.UtcNow
            };
            Messages.Add(processingMessage);

            // 5. Nochmal nach unten scrollen
            ScrollToBottom();

            // 6. Animation starten
            _loadingAnimationTimer.Start();

            var fullResponse = "";
            var processingIndex = Messages.IndexOf(processingMessage);

            try
            {
                // 5. Streaming von Gemini
                await foreach (var chunk in _chatService.SendMessageStreamAsync(_chat.Id, message))
                {
                    fullResponse += chunk;
                    
                    // Streaming-Nachricht live aktualisieren
                    if (processingIndex >= 0)
                    {
                        Messages[processingIndex] = new ChatMessage
                        {
                            Role = "model",
                            Content = fullResponse,
                            Timestamp = DateTime.UtcNow
                        };
                        
                        // Während des Streamings nach unten scrollen
                        ScrollToBottom();
                    }
                }

                // 6. Beide Nachrichten in DB speichern
                await _databaseService.AddMessageAsync(_chat.Id, "user", message);
                await _databaseService.AddMessageAsync(_chat.Id, "model", fullResponse);
                await _databaseService.UpdateChatStatsAsync(_chat.Id);
            }
            catch (Exception ex)
            {
                // 7. Bei Fehlern: Streaming-Nachricht durch Fehlermeldung ersetzen
                if (processingIndex >= 0)
                {
                    var errorMessage = new ChatMessage
                    {
                        Role = "model",
                        Content = $"Fehler: {ex.Message}",
                        Timestamp = DateTime.UtcNow
                    };
                    Messages[processingIndex] = errorMessage;
                }
            }
            finally
            {
                // Animation stoppen
                _loadingAnimationTimer.Stop();
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

        private void ScrollToBottom()
        {
            Task.Delay(100).ContinueWith(_ =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // Finde den ScrollViewer im Visual Tree
                    var mainWindow = System.Windows.Application.Current.MainWindow as Views.MainWindow;
                    if (mainWindow != null)
                    {
                        var scrollViewer = FindScrollViewer(mainWindow);
                        scrollViewer?.ScrollToEnd();
                    }
                });
            });
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

    }
}