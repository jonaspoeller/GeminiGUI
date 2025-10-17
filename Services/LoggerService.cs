using System;
using System.IO;
using System.Text;

namespace GeminiGUI.Services
{
    public class LoggerService : ILoggerService
    {
        private readonly string _logDirectory;
        private readonly string _logFilePath;
        private readonly string _programLogFilePath;
        private readonly object _lockObject = new object();

        public LoggerService()
        {
            // AppData Log-Verzeichnis
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _logDirectory = Path.Combine(appDataPath, "GeminiGUI", "Logs");
            Directory.CreateDirectory(_logDirectory);
            
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            _logFilePath = Path.Combine(_logDirectory, $"gemini_gui_{today}.log");
            
            // Programmordner Log-Datei (einfach lesbar)
            var programDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _programLogFilePath = Path.Combine(programDirectory, $"gemini_gui_log_{today}.txt");
        }

        public void LogInfo(string message)
        {
            WriteLog("INFO", message);
        }

        public void LogWarning(string message)
        {
            WriteLog("WARN", message);
        }

        public void LogError(string message, Exception? exception = null)
        {
            var fullMessage = message;
            if (exception != null)
            {
                fullMessage += $"\nException: {exception.GetType().Name}: {exception.Message}";
                fullMessage += $"\nStack Trace: {exception.StackTrace}";
                if (exception.InnerException != null)
                {
                    fullMessage += $"\nInner Exception: {exception.InnerException.GetType().Name}: {exception.InnerException.Message}";
                }
            }
            WriteLog("ERROR", fullMessage);
        }

        public void LogDebug(string message)
        {
            WriteLog("DEBUG", message);
        }

        public void LogApiRequest(string method, string url, string? requestBody = null)
        {
            var message = $"API Request: {method} {url}";
            if (!string.IsNullOrEmpty(requestBody))
            {
                message += $"\nRequest Body: {requestBody}";
            }
            WriteLog("API_REQ", message);
        }

        public void LogApiResponse(string method, string url, int statusCode, string? responseBody = null)
        {
            var message = $"API Response: {method} {url} - Status: {statusCode}";
            if (!string.IsNullOrEmpty(responseBody))
            {
                message += $"\nResponse Body: {responseBody}";
            }
            WriteLog("API_RES", message);
        }

        public void LogDatabaseOperation(string operation, string? details = null)
        {
            var message = $"Database: {operation}";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" - {details}";
            }
            WriteLog("DB", message);
        }

        public void LogUserAction(string action, string? details = null)
        {
            var message = $"User Action: {action}";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" - {details}";
            }
            WriteLog("USER", message);
        }

        private void WriteLog(string level, string message)
        {
            lock (_lockObject)
            {
                try
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var logEntry = $"[{timestamp}] [{level}] {message}\n";
                    
                    // In AppData schreiben (detailliert)
                    File.AppendAllText(_logFilePath, logEntry, Encoding.UTF8);
                    
                    // In Programmordner schreiben (einfach lesbar)
                    File.AppendAllText(_programLogFilePath, logEntry, Encoding.UTF8);
                    
                    // Auch in die Konsole ausgeben für Debug-Zwecke
                    Console.WriteLine($"[{timestamp}] [{level}] {message}");
                }
                catch (Exception ex)
                {
                    // Fallback: In Event Log oder temporäre Datei schreiben
                    try
                    {
                        var fallbackPath = Path.Combine(Path.GetTempPath(), "gemini_gui_fallback.log");
                        var fallbackEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [LOGGER_ERROR] Failed to write log: {ex.Message}\n";
                        File.AppendAllText(fallbackPath, fallbackEntry, Encoding.UTF8);
                    }
                    catch
                    {
                        // Letzter Fallback: Ignorieren
                    }
                }
            }
        }

        public string GetLogFilePath()
        {
            return _logFilePath;
        }

        public string GetProgramLogFilePath()
        {
            return _programLogFilePath;
        }

        public string[] GetRecentLogEntries(int count = 50)
        {
            try
            {
                // Versuche zuerst die Programmordner-Log-Datei zu lesen
                if (File.Exists(_programLogFilePath))
                {
                    var lines = File.ReadAllLines(_programLogFilePath);
                    var startIndex = Math.Max(0, lines.Length - count);
                    var result = new string[lines.Length - startIndex];
                    Array.Copy(lines, startIndex, result, 0, result.Length);
                    return result;
                }
                
                // Fallback auf AppData-Log-Datei
                if (File.Exists(_logFilePath))
                {
                    var lines = File.ReadAllLines(_logFilePath);
                    var startIndex = Math.Max(0, lines.Length - count);
                    var result = new string[lines.Length - startIndex];
                    Array.Copy(lines, startIndex, result, 0, result.Length);
                    return result;
                }
                
                return new string[0];
            }
            catch
            {
                return new string[0];
            }
        }
    }
}
