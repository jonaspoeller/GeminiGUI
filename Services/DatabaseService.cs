using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using GeminiGUI.Models;

namespace GeminiGUI.Services
{
    public class DatabaseService : IDatabaseService
    {
        private readonly string _connectionString;
        private readonly ILoggerService _logger;
        private SqliteConnection? _connection;
        private readonly string _keyPath;
        private byte[]? _aesKey;
        private Task? _initializationTask;

        public DatabaseService(IConfiguration configuration, ILoggerService logger)
        {
            _logger = logger;
            
            // Datenbank in AppData speichern (benutzer-spezifisch)
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "GeminiGUI");
            Directory.CreateDirectory(appFolder);
            var dbPath = Path.Combine(appFolder, "gemini_chats.db");
            _keyPath = Path.Combine(appFolder, "db_key.dat");
            
            _connectionString = $"Data Source={dbPath}";
            
            _logger.LogInfo($"DatabaseService initialized with connection: {dbPath}");
        }

        private async Task<byte[]> GetOrCreateAesKeyAsync()
        {
            if (_aesKey != null)
                return _aesKey;

            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] GetOrCreateAesKeyAsync START");
            // Run all key operations on background thread to keep UI responsive
            _aesKey = await Task.Run(async () =>
            {
                _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Task.Run START - loading/generating key");
                // Versuche verschlüsselten AES-Schlüssel zu laden
                if (File.Exists(_keyPath))
                {
                    try
                    {
                        _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Loading existing key");
                        var encryptedKey = await File.ReadAllBytesAsync(_keyPath);
                        // Entschlüssele AES-Schlüssel mit DPAPI
                        _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Unprotecting key");
                        var result = ProtectedData.Unprotect(encryptedKey, null, DataProtectionScope.CurrentUser);
                        _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Key unprotected successfully");
                        return result;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[{DateTime.Now:HH:mm:ss.fff}] Failed to load key: {ex.Message}");
                        // Wenn Entschlüsselung fehlschlägt, generiere neuen Schlüssel
                    }
                }

                // Generiere neuen AES-256-Schlüssel (32 Bytes = 256-bit)
                _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Generating new key");
                using var aes = Aes.Create();
                aes.KeySize = 256;
                aes.GenerateKey();
                var newKey = aes.Key;

                // Speichere verschlüsselten AES-Schlüssel mit DPAPI
                _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Protecting new key");
                var encryptedAesKey = ProtectedData.Protect(newKey, null, DataProtectionScope.CurrentUser);
                _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Saving protected key");
                await File.WriteAllBytesAsync(_keyPath, encryptedAesKey);
                _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Key saved successfully");

                return newKey;
            }).ConfigureAwait(false);

            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] GetOrCreateAesKeyAsync END");
            return _aesKey;
        }

        private async Task<string> EncryptContentAsync(string content)
        {
            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] EncryptContentAsync START");
            try
            {
                if (string.IsNullOrEmpty(content))
                    return content;

                _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Getting AES key");
                var aesKey = await GetOrCreateAesKeyAsync().ConfigureAwait(false);
                _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] AES key obtained");

                // Run encryption on background thread to keep UI responsive
                _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Starting encryption Task.Run");
                var result = await Task.Run(() =>
                {
                    _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Encryption Task.Run executing");
                    using var aes = Aes.Create();
                    aes.KeySize = 256;
                    aes.Key = aesKey;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using var encryptor = aes.CreateEncryptor();
                    var plaintextBytes = Encoding.UTF8.GetBytes(content);
                    var encryptedBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

                    // Speichere IV und verschlüsselte Daten zusammen
                    var result = new byte[aes.IV.Length + encryptedBytes.Length];
                    Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
                    Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);

                    _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Encryption Task.Run completed");
                    return Convert.ToBase64String(result);
                }).ConfigureAwait(false);
                _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] EncryptContentAsync END");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to encrypt content: {ex.Message}", ex);
                throw new InvalidOperationException($"Database encryption failed: {ex.Message}", ex);
            }
        }

        private async Task<string> DecryptContentAsync(string encryptedContent)
        {
            try
            {
                if (string.IsNullOrEmpty(encryptedContent))
                    return encryptedContent;

                // Check if content looks like encrypted data (base64)
                if (!encryptedContent.Contains("=") && encryptedContent.Length < 50)
                {
                    // Likely unencrypted content from before encryption was added
                    return encryptedContent;
                }

                var aesKey = await GetOrCreateAesKeyAsync().ConfigureAwait(false);

                // Run decryption on background thread to keep UI responsive
                return await Task.Run(() =>
                {
                    var fullCipher = Convert.FromBase64String(encryptedContent);

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
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to decrypt content: {ex.Message}", ex);
                // If decryption fails, return original content (might be unencrypted)
                return encryptedContent;
            }
        }

        private async Task EnsureInitializedAsync()
        {
            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] EnsureInitializedAsync START");

            if (_connection != null)
            {
                _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Connection already exists");
                return;
            }

            if (_initializationTask != null)
            {
                _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Waiting for existing initialization task");
                await _initializationTask.ConfigureAwait(false);
                _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Existing initialization task completed");
                return;
            }

            // Run initialization on background thread to keep UI responsive
            _initializationTask = Task.Run(async () =>
        {
            try
            {
                _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Task.Run START - initializing database");
                _logger.LogInfo("Initializing database connection");
                _connection = new SqliteConnection(_connectionString);
                await _connection.OpenAsync();
                _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Database connection opened");
                await CreateTablesAsync();
                _logger.LogInfo("Database initialized successfully");
                _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Task.Run END - database initialized");
            }
            catch (Exception ex)
            {
                    _logger.LogError($"Failed to initialize database: {ex.Message}", ex);
                    throw new InvalidOperationException($"Failed to initialize database. Please check your permissions and try again.\n\nError: {ex.Message}", ex);
                }
            });

            // Wait for initialization to complete
            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Waiting for initialization task to complete");
            await _initializationTask.ConfigureAwait(false);
            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] EnsureInitializedAsync END");
        }

        public async Task InitializeAsync()
        {
            await EnsureInitializedAsync();
        }

        public async Task PrepareEncryptionAsync()
        {
            // Pre-generate or load the AES key to avoid blocking on first message
            await GetOrCreateAesKeyAsync().ConfigureAwait(false);
        }

        public async Task CloseAsync()
        {
            if (_connection != null)
            {
                await _connection.CloseAsync();
                _connection.Dispose();
                _connection = null;
                _initializationTask = null;
                _aesKey = null;
            }
        }

        private async Task CreateTablesAsync()
        {
            var createChatsTable = @"
                CREATE TABLE IF NOT EXISTS Chats (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    MessageCount INTEGER DEFAULT 0,
                    TotalTokens INTEGER DEFAULT 0
                )";

            var createMessagesTable = @"
                CREATE TABLE IF NOT EXISTS ChatMessages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ChatId INTEGER NOT NULL,
                    Role TEXT NOT NULL,
                    Content TEXT NOT NULL,
                    Timestamp TEXT NOT NULL,
                    TokenCount INTEGER DEFAULT 0,
                    FOREIGN KEY (ChatId) REFERENCES Chats(Id) ON DELETE CASCADE
                )";

            var createIndexes = @"
                CREATE INDEX IF NOT EXISTS IX_ChatMessages_ChatId ON ChatMessages(ChatId);
                CREATE INDEX IF NOT EXISTS IX_ChatMessages_Timestamp ON ChatMessages(Timestamp);
                CREATE INDEX IF NOT EXISTS IX_Chats_UpdatedAt ON Chats(UpdatedAt);
            ";

            using var command = _connection!.CreateCommand();
            command.CommandText = createChatsTable;
            await command.ExecuteNonQueryAsync();

            command.CommandText = createMessagesTable;
            await command.ExecuteNonQueryAsync();

            command.CommandText = createIndexes;
            await command.ExecuteNonQueryAsync();
            
            // Alte Nachrichten ohne Timestamp bereinigen
            await CleanupOldMessagesAsync();
        }

        public async Task<List<Chat>> GetAllChatsAsync()
        {
            var chats = new List<Chat>();
            using var command = _connection!.CreateCommand();
            command.CommandText = "SELECT * FROM Chats ORDER BY UpdatedAt DESC";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                chats.Add(new Chat
                {
                    Id = reader.GetInt32("Id"),
                    Title = reader.GetString("Title"),
                    CreatedAt = DateTime.Parse(reader.GetString("CreatedAt")),
                    UpdatedAt = DateTime.Parse(reader.GetString("UpdatedAt")),
                    MessageCount = reader.GetInt32("MessageCount"),
                    TotalTokens = reader.GetInt64("TotalTokens")
                });
            }

            return chats;
        }

        public async Task<Chat?> GetChatByIdAsync(int id)
        {
            using var command = _connection!.CreateCommand();
            command.CommandText = "SELECT * FROM Chats WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Chat
                {
                    Id = reader.GetInt32("Id"),
                    Title = reader.GetString("Title"),
                    CreatedAt = DateTime.Parse(reader.GetString("CreatedAt")),
                    UpdatedAt = DateTime.Parse(reader.GetString("UpdatedAt")),
                    MessageCount = reader.GetInt32("MessageCount"),
                    TotalTokens = reader.GetInt64("TotalTokens")
                };
            }

            return null;
        }

        public async Task<Chat> CreateChatAsync(string title)
        {
            using var command = _connection!.CreateCommand();
            command.CommandText = @"
                INSERT INTO Chats (Title, CreatedAt, UpdatedAt, MessageCount, TotalTokens)
                VALUES (@title, @createdAt, @updatedAt, 0, 0);
                SELECT last_insert_rowid();";

            var now = DateTime.UtcNow.ToString("O");
            command.Parameters.AddWithValue("@title", title);
            command.Parameters.AddWithValue("@createdAt", now);
            command.Parameters.AddWithValue("@updatedAt", now);

            var chatId = Convert.ToInt32(await command.ExecuteScalarAsync());
            return new Chat
            {
                Id = chatId,
                Title = title,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                MessageCount = 0,
                TotalTokens = 0
            };
        }

        public async Task UpdateChatAsync(Chat chat)
        {
            using var command = _connection!.CreateCommand();
            command.CommandText = @"
                UPDATE Chats 
                SET Title = @title, UpdatedAt = @updatedAt, 
                    MessageCount = @messageCount, TotalTokens = @totalTokens
                WHERE Id = @id";

            command.Parameters.AddWithValue("@id", chat.Id);
            command.Parameters.AddWithValue("@title", chat.Title);
            command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("@messageCount", chat.MessageCount);
            command.Parameters.AddWithValue("@totalTokens", chat.TotalTokens);

            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteChatAsync(int id)
        {
            using var command = _connection!.CreateCommand();
            command.CommandText = "DELETE FROM Chats WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<List<ChatMessage>> GetChatMessagesAsync(int chatId)
        {
            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] GetChatMessagesAsync START for chatId: {chatId}");
            var messages = new List<ChatMessage>();
            using var command = _connection!.CreateCommand();
            command.CommandText = "SELECT * FROM ChatMessages WHERE ChatId = @chatId ORDER BY Timestamp ASC";
            command.Parameters.AddWithValue("@chatId", chatId);

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] ExecuteReader completed");
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                try
                {
                    var encryptedContent = reader.GetString("Content");
                    var decryptedContent = await DecryptContentAsync(encryptedContent).ConfigureAwait(false);
                    
                messages.Add(new ChatMessage
                {
                    Id = reader.GetInt32("Id"),
                    ChatId = reader.GetInt32("ChatId"),
                    Role = reader.GetString("Role"),
                        Content = decryptedContent,
                    Timestamp = DateTime.Parse(reader.GetString("Timestamp")),
                    TokenCount = reader.GetInt32("TokenCount")
                });
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to decrypt message {reader.GetInt32("Id")}: {ex.Message}", ex);
                    // Skip this message if decryption fails
                }
            }

            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] GetChatMessagesAsync END - loaded {messages.Count} messages");
            return messages;
        }

        public async Task<ChatMessage> AddMessageAsync(int chatId, string role, string content, int tokenCount = 0)
        {
            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] AddMessageAsync START");
            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Calling EnsureInitializedAsync");
            await EnsureInitializedAsync().ConfigureAwait(false);
            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Database initialized");
            
            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Creating command");
            using var command = _connection!.CreateCommand();
            command.CommandText = @"
                INSERT INTO ChatMessages (ChatId, Role, Content, Timestamp, TokenCount)
                VALUES (@chatId, @role, @content, @timestamp, @tokenCount);
                SELECT last_insert_rowid();";

            // Verschlüssele den Inhalt vor dem Speichern
            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Starting encryption");
            var encryptedContent = await EncryptContentAsync(content).ConfigureAwait(false);
            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Encryption completed");

            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Adding parameters");
            command.Parameters.AddWithValue("@chatId", chatId);
            command.Parameters.AddWithValue("@role", role);
            command.Parameters.AddWithValue("@content", encryptedContent);
            command.Parameters.AddWithValue("@timestamp", DateTime.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("@tokenCount", tokenCount);

            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Executing command");
            var messageId = Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false));
            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Command executed, messageId: {messageId}");
            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] AddMessageAsync END - messageId: {messageId}");
            return new ChatMessage
            {
                Id = messageId,
                ChatId = chatId,
                Role = role,
                Content = content, // Original-Content zurückgeben (nicht verschlüsselt)
                Timestamp = DateTime.UtcNow,
                TokenCount = tokenCount
            };
        }

        public async Task DeleteMessageAsync(int messageId)
        {
            using var command = _connection!.CreateCommand();
            command.CommandText = "DELETE FROM ChatMessages WHERE Id = @id";
            command.Parameters.AddWithValue("@id", messageId);
            await command.ExecuteNonQueryAsync();
        }

        public async Task UpdateChatStatsAsync(int chatId)
        {
            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] UpdateChatStatsAsync START");
            await EnsureInitializedAsync().ConfigureAwait(false);
            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Database initialized");
            
            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Creating command");
            using var command = _connection!.CreateCommand();
            command.CommandText = @"
                UPDATE Chats 
                SET MessageCount = (SELECT COUNT(*) FROM ChatMessages WHERE ChatId = @chatId),
                    TotalTokens = (SELECT COALESCE(SUM(TokenCount), 0) FROM ChatMessages WHERE ChatId = @chatId),
                    UpdatedAt = @updatedAt
                WHERE Id = @chatId";

            command.Parameters.AddWithValue("@chatId", chatId);
            command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("O"));
            
            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Executing command");
            await command.ExecuteNonQueryAsync();
            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] Command executed");
            _logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] UpdateChatStatsAsync END");
        }

        private async Task CleanupOldMessagesAsync()
        {
            try
            {
                // Nachrichten ohne gültigen Timestamp finden und mit aktueller Zeit aktualisieren
                using var command = _connection!.CreateCommand();
                command.CommandText = @"
                    UPDATE ChatMessages 
                    SET Timestamp = @currentTime 
                    WHERE Timestamp = '0001-01-01T00:00:00.0000000Z' OR Timestamp = '' OR Timestamp IS NULL";

                command.Parameters.AddWithValue("@currentTime", DateTime.UtcNow.ToString("O"));
                var updatedRows = await command.ExecuteNonQueryAsync();
                
                if (updatedRows > 0)
                {
                    _logger.LogInfo($"Updated {updatedRows} messages with missing timestamps");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error cleaning up old messages", ex);
            }
        }
    }
}