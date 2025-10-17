using System;

namespace GeminiGUI.Services
{
    public interface ILoggerService
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception? exception = null);
        void LogDebug(string message);
        void LogApiRequest(string method, string url, string? requestBody = null);
        void LogApiResponse(string method, string url, int statusCode, string? responseBody = null);
        void LogDatabaseOperation(string operation, string? details = null);
        void LogUserAction(string action, string? details = null);
        string[] GetRecentLogEntries(int count = 50);
        string GetProgramLogFilePath();
    }
}
