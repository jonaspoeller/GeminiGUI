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
        private readonly string _keyPath;
        private byte[]? _aesKey;

        public ConfigurationService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "GeminiGUI");
            Directory.CreateDirectory(appFolder);
            _configPath = Path.Combine(appFolder, "config.dat");
            _keyPath = Path.Combine(appFolder, "key.dat");
        }

        private async Task<byte[]> GetOrCreateAesKeyAsync()
        {
            if (_aesKey != null)
                return _aesKey;

            // Versuche verschlüsselten AES-Schlüssel zu laden
            if (File.Exists(_keyPath))
            {
                try
                {
                    var encryptedKey = await File.ReadAllBytesAsync(_keyPath);
                    // Entschlüssele AES-Schlüssel mit DPAPI
                    _aesKey = ProtectedData.Unprotect(encryptedKey, null, DataProtectionScope.CurrentUser);
                    return _aesKey;
                }
                catch
                {
                    // Wenn Entschlüsselung fehlschlägt, generiere neuen Schlüssel
                }
            }

            // Generiere neuen AES-256-Schlüssel (32 Bytes = 256-bit)
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.GenerateKey();
            _aesKey = aes.Key;

            // Speichere verschlüsselten AES-Schlüssel mit DPAPI
            var encryptedAesKey = ProtectedData.Protect(_aesKey, null, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(_keyPath, encryptedAesKey);

            return _aesKey;
        }

        private async Task<string> EncryptWithAesAsync(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext))
                return plaintext;

            var aesKey = await GetOrCreateAesKeyAsync();

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key = aesKey;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var encryptedBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

            // Speichere IV und verschlüsselte Daten zusammen
            var result = new byte[aes.IV.Length + encryptedBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);

            return Convert.ToBase64String(result);
        }

        private async Task<string> DecryptWithAesAsync(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return encryptedText;

            var aesKey = await GetOrCreateAesKeyAsync();

            var fullCipher = Convert.FromBase64String(encryptedText);

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key = aesKey;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            // Extrahiere IV
            var iv = new byte[aes.IV.Length];
            var cipher = new byte[fullCipher.Length - iv.Length];
            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var decryptedBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

            return Encoding.UTF8.GetString(decryptedBytes);
        }

        public async Task<string?> GetApiKeyAsync()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    return null;
                }

                var encryptedData = await File.ReadAllTextAsync(_configPath);
                var apiKey = await DecryptWithAesAsync(encryptedData);
                
                // Return empty string as null if decrypted key is empty
                return string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
            }
            catch (Exception)
            {
                // If decryption fails (e.g., different user or corrupted data), return null
                return null;
            }
        }

        public async Task SetApiKeyAsync(string apiKey)
        {
            try
            {
                var encryptedData = await EncryptWithAesAsync(apiKey);
                await File.WriteAllTextAsync(_configPath, encryptedData);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save API key: {ex.Message}", ex);
            }
        }

        public async Task<bool> HasApiKeyAsync()
        {
            // Check if API key exists in configuration
            var apiKey = await GetApiKeyAsync();
            return !string.IsNullOrEmpty(apiKey);
        }
    }
}