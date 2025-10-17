using System.Collections.Generic;
using System.Threading.Tasks;
using GeminiGUI.Models;

namespace GeminiGUI.Services
{
    public interface IGeminiService
    {
        void SetApiKey(string apiKey);
        Task<string> SendMessageAsync(string message, List<Models.ChatMessage> chatHistory);
        IAsyncEnumerable<string> SendMessageStreamAsync(string message, List<Models.ChatMessage> chatHistory);
        Task<bool> TestConnectionAsync();
    }
}