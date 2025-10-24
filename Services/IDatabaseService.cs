using System.Collections.Generic;
using System.Threading.Tasks;
using GeminiGUI.Models;

namespace GeminiGUI.Services
{
    public interface IDatabaseService
    {
        Task InitializeAsync();
        Task PrepareEncryptionAsync();
        Task CloseAsync();
        Task<List<Chat>> GetAllChatsAsync();
        Task<Chat?> GetChatByIdAsync(int id);
        Task<Chat> CreateChatAsync(string title);
        Task UpdateChatAsync(Chat chat);
        Task DeleteChatAsync(int id);
        Task<List<ChatMessage>> GetChatMessagesAsync(int chatId);
        Task<ChatMessage> AddMessageAsync(int chatId, string role, string content, int tokenCount = 0);
        Task DeleteMessageAsync(int messageId);
        Task UpdateChatStatsAsync(int chatId);
    }
}