using System.Collections.Generic;
using System.Threading.Tasks;
using GeminiGUI.Models;

namespace GeminiGUI.Services
{
    public interface IChatService
    {
        Task<List<Chat>> GetAllChatsAsync();
        Task<Chat> CreateNewChatAsync(string title);
        Task<Chat?> LoadChatAsync(int chatId);
        Task DeleteChatAsync(int chatId);
        Task<ChatMessage> SendMessageAsync(int chatId, string message);
        IAsyncEnumerable<string> SendMessageStreamAsync(int chatId, string message);
        Task<List<ChatMessage>> GetChatMessagesAsync(int chatId);
        Task UpdateChatTitleAsync(int chatId, string newTitle);
    }
}