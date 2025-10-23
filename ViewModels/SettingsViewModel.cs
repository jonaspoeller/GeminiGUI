using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GeminiGUI.Services;

namespace GeminiGUI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IConfigurationService _configService;
        private readonly IGeminiService _geminiService;

        [ObservableProperty]
        private string _apiKey = string.Empty;

        [ObservableProperty]
        private bool _isApiKeyVisible;

        [ObservableProperty]
        private bool _isTestingConnection;

        [ObservableProperty]
        private string _connectionStatus = "Nicht getestet";

        [ObservableProperty]
        private bool _hasApiKey;

        [ObservableProperty]
        private bool _isApiKeySet;

        [ObservableProperty]
        private string _logContent = string.Empty;

        [ObservableProperty]
        private bool _isLogViewerOpen;

        private readonly ILoggerService _loggerService;

        public SettingsViewModel(IConfigurationService configService, IGeminiService geminiService, ILoggerService loggerService)
        {
            _configService = configService;
            _geminiService = geminiService;
            _loggerService = loggerService;
            LoadApiKeyAsync();
        }

        [RelayCommand]
        private async Task SaveApiKeyAsync()
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
                return;

            try
            {
                await _configService.SetApiKeyAsync(ApiKey);
                _geminiService.SetApiKey(ApiKey);
                HasApiKey = true;
                ConnectionStatus = "API-Schlüssel gespeichert";
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Fehler beim Speichern: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task TestConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                ConnectionStatus = "Kein API-Schlüssel eingegeben";
                return;
            }

            IsTestingConnection = true;
            ConnectionStatus = "Teste Verbindung...";

            try
            {
                _geminiService.SetApiKey(ApiKey);
                var isConnected = await _geminiService.TestConnectionAsync();
                
                if (isConnected)
                {
                    ConnectionStatus = "Verbindung erfolgreich";
                    await _configService.SetApiKeyAsync(ApiKey);
                    HasApiKey = true;
                }
                else
                {
                    ConnectionStatus = "Verbindung fehlgeschlagen";
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Fehler: {ex.Message}";
            }
            finally
            {
                IsTestingConnection = false;
            }
        }

        [RelayCommand]
        private void ToggleApiKeyVisibility()
        {
            IsApiKeyVisible = !IsApiKeyVisible;
        }

        [RelayCommand]
        private async Task ClearApiKeyAsync()
        {
            try
            {
                await _configService.SetApiKeyAsync(string.Empty);
                _geminiService.SetApiKey(string.Empty); // API-Key auch aus GeminiService entfernen
                ApiKey = string.Empty;
                HasApiKey = false;
                ConnectionStatus = "API-Schlüssel gelöscht";
                _loggerService.LogUserAction("API key cleared");
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Fehler beim Löschen: {ex.Message}";
                _loggerService.LogError("Failed to clear API key", ex);
            }
        }

        [RelayCommand]
        private void OpenLogViewer()
        {
            try
            {
                var logEntries = _loggerService.GetRecentLogEntries(100);
                LogContent = string.Join("\n", logEntries);
                IsLogViewerOpen = true;
                _loggerService.LogUserAction("Log viewer opened");
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to open log viewer", ex);
            }
        }

        [RelayCommand]
        private void CloseLogViewer()
        {
            IsLogViewerOpen = false;
            LogContent = string.Empty;
        }

        [RelayCommand]
        private void RefreshLogs()
        {
            try
            {
                var logEntries = _loggerService.GetRecentLogEntries(100);
                LogContent = string.Join("\n", logEntries);
                _loggerService.LogUserAction("Logs refreshed");
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to refresh logs", ex);
            }
        }

        [RelayCommand]
        private void OpenLogFileInExplorer()
        {
            try
            {
                var logFilePath = _loggerService.GetProgramLogFilePath();
                if (File.Exists(logFilePath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{logFilePath}\"");
                    _loggerService.LogUserAction("Log file opened in explorer");
                }
                else
                {
                    _loggerService.LogWarning("Log file not found, opening program directory");
                    var programDirectory = Path.GetDirectoryName(logFilePath);
                    if (Directory.Exists(programDirectory))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", programDirectory);
                    }
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to open log file in explorer", ex);
            }
        }

        private async Task LoadApiKeyAsync()
        {
            try
            {
                var existingApiKey = await _configService.GetApiKeyAsync();
                HasApiKey = !string.IsNullOrEmpty(existingApiKey);
                
                if (HasApiKey)
                {
                    ApiKey = existingApiKey!;
                    IsApiKeySet = true;
                    ConnectionStatus = "API-Schlüssel geladen";
                }
                else
                {
                    ApiKey = string.Empty;
                    IsApiKeySet = false;
                    ConnectionStatus = "Kein API-Schlüssel";
                }
            }
            catch (Exception ex)
            {
                HasApiKey = false;
                ApiKey = string.Empty;
                IsApiKeySet = false;
                ConnectionStatus = $"Fehler beim Laden: {ex.Message}";
            }
        }

        // Method to refresh API key display when settings window is opened
        public async Task RefreshApiKeyAsync()
        {
            await LoadApiKeyAsync();
        }
    }
}