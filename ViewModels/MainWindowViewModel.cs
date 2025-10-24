using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GeminiGUI.Models;
using GeminiGUI.Services;

namespace GeminiGUI.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly IChatService _chatService;
        private readonly IDatabaseService _databaseService;
        private readonly IConfigurationService _configService;
        private readonly ILoggerService _logger;

        [ObservableProperty]
        private ObservableCollection<Chat> _chats = new();

        [ObservableProperty]
        private Chat? _selectedChat;

        [ObservableProperty]
        private ChatViewModel? _currentChatViewModel;

        [ObservableProperty]
        private bool _isSettingsOpen;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private SettingsViewModel? _settingsViewModel;

        [ObservableProperty]
        private bool _isInputEnabled;

        [ObservableProperty]
        private string _apiKeyStatus = "No API key";

        [ObservableProperty]
        private bool _hasActiveChat;

        [ObservableProperty]
        private ObservableCollection<string> _availableModels = new()
        {
            "2.5 Pro",
            "Flash Latest"
        };

        [ObservableProperty]
        private string _selectedModel = "2.5 Pro";

        public MainWindowViewModel(IChatService chatService, IDatabaseService databaseService, IConfigurationService configService, SettingsViewModel settingsViewModel, ILoggerService logger)
        {
            _chatService = chatService;
            _databaseService = databaseService;
            _configService = configService;
            _settingsViewModel = settingsViewModel;
            _logger = logger;
            _isInputEnabled = false; // Disabled by default
            _ = LoadChatsAsync(); // Fire and forget
            UpdateInputEnabled(); // Initial state
            _ = UpdateApiKeyStatusAsync(); // Check API key status
        }

        private void UpdateInputEnabled()
        {
            IsInputEnabled = SelectedChat != null && (CurrentChatViewModel?.IsLoading != true);
            HasActiveChat = SelectedChat != null;
        }

        private async Task UpdateApiKeyStatusAsync()
        {
            try
            {
                var hasApiKey = await _configService.HasApiKeyAsync();
                ApiKeyStatus = hasApiKey ? "API key loaded" : "No API key";
            }
            catch
            {
                ApiKeyStatus = "No API key";
            }
        }

        [RelayCommand]
        private async Task CreateNewChatAsync()
        {
            try
            {
                StatusMessage = "Creating new chat...";
                var newChat = await _chatService.CreateNewChatAsync("New Chat");
                Chats.Insert(0, newChat);
                SelectedChat = newChat;
                UpdateInputEnabled();
                HasActiveChat = true; // Explicitly set HasActiveChat to true
                StatusMessage = "New chat created";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task DeleteChatAsync(Chat chat)
        {
            if (chat == null) return;

            try
            {
                StatusMessage = "Deleting chat...";
                await _chatService.DeleteChatAsync(chat.Id);
                Chats.Remove(chat);
                
                if (SelectedChat?.Id == chat.Id)
                {
                    SelectedChat = Chats.FirstOrDefault();
                    UpdateInputEnabled();
                }
                
                StatusMessage = "Chat deleted";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting chat: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task OpenSettings()
        {
            var settingsWindow = new Views.SettingsWindow();
            settingsWindow.Owner = System.Windows.Application.Current.MainWindow;
            
            // Refresh API key before showing settings
            if (settingsWindow.DataContext is SettingsViewModel settingsViewModel)
            {
                await settingsViewModel.RefreshApiKeyAsync();
            }
            
            settingsWindow.ShowDialog();
            
            // Update API key status after settings window is closed
            await UpdateApiKeyStatusAsync();
        }

        [RelayCommand]
        private void CloseSettings()
        {
            // Not needed anymore - settings are in separate window
        }

        partial void OnSelectedChatChanged(Chat? value)
        {
            // Dispose old ChatViewModel to prevent memory leaks
            if (CurrentChatViewModel != null)
            {
                CurrentChatViewModel.LoadingStateChanged -= OnLoadingStateChanged;
                CurrentChatViewModel.Dispose();
                CurrentChatViewModel = null;
            }
            
            if (value != null)
            {
                CurrentChatViewModel = new ChatViewModel(_chatService, _databaseService, value, _logger);
                CurrentChatViewModel.LoadingStateChanged += OnLoadingStateChanged;
            }
            
            UpdateInputEnabled();
            HasActiveChat = value != null; // Explicitly update HasActiveChat
        }

        private void OnLoadingStateChanged(object? sender, bool isLoading)
        {
            UpdateInputEnabled();
        }

        private async Task LoadChatsAsync()
        {
            try
            {
                StatusMessage = "Loading chats...";
                var chats = await _chatService.GetAllChatsAsync();
                Chats.Clear();
                foreach (var chat in chats)
                {
                    Chats.Add(chat);
                }
                
                if (Chats.Any())
                {
                    SelectedChat = Chats.First();
                }
                
                StatusMessage = $"{Chats.Count} chats loaded";
                UpdateInputEnabled(); // Update input state after loading chats
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading chats: {ex.Message}";
            }
        }

    }
}