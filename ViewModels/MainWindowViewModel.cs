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

        [ObservableProperty]
        private ObservableCollection<Chat> _chats = new();

        [ObservableProperty]
        private Chat? _selectedChat;

        [ObservableProperty]
        private ChatViewModel? _currentChatViewModel;

        [ObservableProperty]
        private bool _isSettingsOpen;

        [ObservableProperty]
        private string _statusMessage = "Bereit";

        [ObservableProperty]
        private SettingsViewModel? _settingsViewModel;

        [ObservableProperty]
        private bool _isInputEnabled;

        public MainWindowViewModel(IChatService chatService, IDatabaseService databaseService, IConfigurationService configService, SettingsViewModel settingsViewModel)
        {
            _chatService = chatService;
            _databaseService = databaseService;
            _configService = configService;
            _settingsViewModel = settingsViewModel;
            _isInputEnabled = false; // Standardmäßig deaktiviert
            _ = LoadChatsAsync(); // Fire and forget
            UpdateInputEnabled(); // Initial state
        }

        private void UpdateInputEnabled()
        {
            IsInputEnabled = SelectedChat != null && (CurrentChatViewModel?.IsLoading != true);
        }

        [RelayCommand]
        private async Task CreateNewChatAsync()
        {
            try
            {
                StatusMessage = "Erstelle neuen Chat...";
                var newChat = await _chatService.CreateNewChatAsync("Neuer Chat");
                Chats.Insert(0, newChat);
                SelectedChat = newChat;
                UpdateInputEnabled();
                StatusMessage = "Neuer Chat erstellt";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fehler: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task DeleteChatAsync(Chat chat)
        {
            if (chat == null) return;

            try
            {
                StatusMessage = "Lösche Chat...";
                await _chatService.DeleteChatAsync(chat.Id);
                Chats.Remove(chat);
                
                if (SelectedChat?.Id == chat.Id)
                {
                    SelectedChat = Chats.FirstOrDefault();
                    UpdateInputEnabled();
                }
                
                StatusMessage = "Chat gelöscht";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fehler beim Löschen: {ex.Message}";
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
        }

        [RelayCommand]
        private void CloseSettings()
        {
            // Not needed anymore - settings are in separate window
        }

        partial void OnSelectedChatChanged(Chat? value)
        {
            if (value != null)
            {
                CurrentChatViewModel = new ChatViewModel(_chatService, _databaseService, value);
                CurrentChatViewModel.LoadingStateChanged += OnLoadingStateChanged;
            }
            else
            {
                if (CurrentChatViewModel != null)
                {
                    CurrentChatViewModel.LoadingStateChanged -= OnLoadingStateChanged;
                }
                CurrentChatViewModel = null;
            }
            UpdateInputEnabled();
        }

        private void OnLoadingStateChanged(object? sender, bool isLoading)
        {
            UpdateInputEnabled();
        }

        private async Task LoadChatsAsync()
        {
            try
            {
                StatusMessage = "Lade Chats...";
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
                
                StatusMessage = $"{Chats.Count} Chats geladen";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fehler beim Laden: {ex.Message}";
            }
        }
    }
}