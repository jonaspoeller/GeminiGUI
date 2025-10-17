using System;
using System.Windows;

namespace GeminiGUI.Services
{
    public static class ExceptionHandler
    {
        private static ILoggerService? _logger;

        public static void Initialize(ILoggerService logger)
        {
            _logger = logger;
        }

        public static void HandleException(Exception ex, string context = "")
        {
            var message = $"Ein Fehler ist aufgetreten: {ex.Message}";
            if (!string.IsNullOrEmpty(context))
            {
                message = $"{context}: {ex.Message}";
            }

            // Detailliertes Logging
            _logger?.LogError($"Exception in {context}", ex);
            
            // Benutzerfreundliche Meldung
            MessageBox.Show(message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static void HandleApiException(Exception ex)
        {
            var message = ex.Message.Contains("401") 
                ? "Ungültiger API-Schlüssel. Bitte überprüfen Sie Ihre Einstellungen."
                : ex.Message.Contains("403")
                ? "Zugriff verweigert. Überprüfen Sie Ihre API-Berechtigungen."
                : ex.Message.Contains("429")
                ? "Zu viele Anfragen. Bitte warten Sie einen Moment."
                : $"API-Fehler: {ex.Message}";

            // Detailliertes Logging
            _logger?.LogError("API Exception", ex);
            
            // Benutzerfreundliche Meldung
            MessageBox.Show(message, "API-Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
