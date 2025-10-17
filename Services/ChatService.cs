using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GeminiGUI.Models;
using System.Collections;

namespace GeminiGUI.Services
{
    public class ChatService : IChatService
    {
        private readonly IDatabaseService _databaseService;
        private readonly IGeminiService _geminiService;

        public ChatService(IDatabaseService databaseService, IGeminiService geminiService)
        {
            _databaseService = databaseService;
            _geminiService = geminiService;
        }

        public async Task<List<Chat>> GetAllChatsAsync()
        {
            return await _databaseService.GetAllChatsAsync();
        }

        public async Task<Chat> CreateNewChatAsync(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                title = "Neuer Chat";

            return await _databaseService.CreateChatAsync(title);
        }

        public async Task<Chat?> LoadChatAsync(int chatId)
        {
            return await _databaseService.GetChatByIdAsync(chatId);
        }

        public async Task DeleteChatAsync(int chatId)
        {
            await _databaseService.DeleteChatAsync(chatId);
        }

        public async Task<ChatMessage> SendMessageAsync(int chatId, string message)
        {
            // Chat-Verlauf laden (ohne die neue Nachricht)
            var chatHistory = await _databaseService.GetChatMessagesAsync(chatId);
            
            // An Gemini senden (mit Verlauf + neue Nachricht)
            var response = await _geminiService.SendMessageAsync(message, chatHistory);
            
            // Benutzernachricht speichern
            var userMessage = await _databaseService.AddMessageAsync(chatId, "user", message);
            
            // Antwort speichern
            var assistantMessage = await _databaseService.AddMessageAsync(chatId, "model", response);
            
            // Chat-Statistiken aktualisieren
            await _databaseService.UpdateChatStatsAsync(chatId);
            
            return assistantMessage;
        }

        public async Task<ChatMessage> SendMessageOnlyAsync(int chatId, string message)
        {
            // Chat-Verlauf laden (ohne die neue Nachricht)
            var chatHistory = await _databaseService.GetChatMessagesAsync(chatId);
            
            // An Gemini senden (mit Verlauf + neue Nachricht)
            var response = await _geminiService.SendMessageAsync(message, chatHistory);
            
            // Nur die Antwort zurückgeben, ohne in DB zu speichern
            return new ChatMessage
            {
                Role = "model",
                Content = response,
                Timestamp = DateTime.UtcNow
            };
        }

        public async IAsyncEnumerable<string> SendMessageStreamAsync(int chatId, string message)
        {
            // Chat-Verlauf laden (ohne die neue Nachricht)
            var chatHistory = await _databaseService.GetChatMessagesAsync(chatId);
            
            // Streaming von Gemini
            await foreach (var chunk in _geminiService.SendMessageStreamAsync(message, chatHistory))
            {
                yield return chunk;
            }
        }

        public async Task<List<ChatMessage>> GetChatMessagesAsync(int chatId)
        {
            return await _databaseService.GetChatMessagesAsync(chatId);
        }

        public async Task UpdateChatTitleAsync(int chatId, string newTitle)
        {
            var chat = await _databaseService.GetChatByIdAsync(chatId);
            if (chat != null)
            {
                chat.Title = newTitle;
                await _databaseService.UpdateChatAsync(chat);
            }
        }
    }
}