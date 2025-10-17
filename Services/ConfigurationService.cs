using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace GeminiGUI.Services
{
    public class ConfigurationService : IConfigurationService
    {
        private readonly string _configPath;
        private readonly byte[] _entropy;

        public ConfigurationService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "GeminiGUI");
            Directory.CreateDirectory(appFolder);
            _configPath = Path.Combine(appFolder, "config.dat");
            
            // Einfacher Entropy für lokale Verschlüsselung
            _entropy = Encoding.UTF8.GetBytes("GeminiGUI_2024_SecretKey");
        }

        public async Task<string?> GetApiKeyAsync()
        {
            // Hardcoded API key
            return "AIzaSyCS_U3ENM3suuOGHc6D4zJT-HAP_RzhC14";
        }

        public async Task SetApiKeyAsync(string apiKey)
        {
            try
            {
                var data = Encoding.UTF8.GetBytes(apiKey);
                var encryptedData = ProtectedData.Protect(data, _entropy, DataProtectionScope.CurrentUser);
                await File.WriteAllBytesAsync(_configPath, encryptedData);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Fehler beim Speichern des API-Schlüssels: {ex.Message}", ex);
            }
        }

        public async Task<bool> HasApiKeyAsync()
        {
            // Always return true since we have a hardcoded API key
            return true;
        }
    }
}