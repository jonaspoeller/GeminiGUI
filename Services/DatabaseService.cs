using System;
using System.Collections.Generic;
using System.Data;
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

        public DatabaseService(IConfiguration configuration, ILoggerService logger)
        {
            _connectionString = configuration.GetConnectionString("Default") 
                ?? "Data Source=gemini_chats.db";
            _logger = logger;
            
            _logger.LogInfo($"DatabaseService initialized with connection: {_connectionString}");
        }

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInfo("Initializing database connection");
                _connection = new SqliteConnection(_connectionString);
                await _connection.OpenAsync();
                await CreateTablesAsync();
                _logger.LogInfo("Database initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize database", ex);
                throw;
            }
        }

        public async Task CloseAsync()
        {
            if (_connection != null)
            {
                await _connection.CloseAsync();
                _connection.Dispose();
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
            var messages = new List<ChatMessage>();
            using var command = _connection!.CreateCommand();
            command.CommandText = "SELECT * FROM ChatMessages WHERE ChatId = @chatId ORDER BY Timestamp ASC";
            command.Parameters.AddWithValue("@chatId", chatId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                messages.Add(new ChatMessage
                {
                    Id = reader.GetInt32("Id"),
                    ChatId = reader.GetInt32("ChatId"),
                    Role = reader.GetString("Role"),
                    Content = reader.GetString("Content"),
                    Timestamp = DateTime.Parse(reader.GetString("Timestamp")),
                    TokenCount = reader.GetInt32("TokenCount")
                });
            }

            return messages;
        }

        public async Task<ChatMessage> AddMessageAsync(int chatId, string role, string content, int tokenCount = 0)
        {
            using var command = _connection!.CreateCommand();
            command.CommandText = @"
                INSERT INTO ChatMessages (ChatId, Role, Content, Timestamp, TokenCount)
                VALUES (@chatId, @role, @content, @timestamp, @tokenCount);
                SELECT last_insert_rowid();";

            command.Parameters.AddWithValue("@chatId", chatId);
            command.Parameters.AddWithValue("@role", role);
            command.Parameters.AddWithValue("@content", content);
            command.Parameters.AddWithValue("@timestamp", DateTime.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("@tokenCount", tokenCount);

            var messageId = Convert.ToInt32(await command.ExecuteScalarAsync());
            return new ChatMessage
            {
                Id = messageId,
                ChatId = chatId,
                Role = role,
                Content = content,
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
            using var command = _connection!.CreateCommand();
            command.CommandText = @"
                UPDATE Chats 
                SET MessageCount = (SELECT COUNT(*) FROM ChatMessages WHERE ChatId = @chatId),
                    TotalTokens = (SELECT COALESCE(SUM(TokenCount), 0) FROM ChatMessages WHERE ChatId = @chatId),
                    UpdatedAt = @updatedAt
                WHERE Id = @chatId";

            command.Parameters.AddWithValue("@chatId", chatId);
            command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
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