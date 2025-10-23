using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using GeminiGUI.Models;

namespace GeminiGUI.Services
{
    public class GeminiService : IGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILoggerService _logger;
        private string? _apiKey;
        private readonly string _baseUrl;

        public GeminiService(HttpClient httpClient, IConfiguration configuration, ILoggerService logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _baseUrl = _configuration["GeminiAPI:BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta";
        }

        public void SetApiKey(string apiKey)
        {
            _apiKey = apiKey;
        }

        public async Task<string> SendMessageAsync(string message, List<ChatMessage> chatHistory)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogError("API-Schlüssel nicht gesetzt");
                throw new InvalidOperationException("API-Schlüssel nicht gesetzt");
            }


            var request = new GeminiRequest
            {
                Contents = new List<Content>()
            };

            if (chatHistory.Count == 0)
            {
                request.Contents.Add(new Content
                {
                    Role = "user",
                    Parts = new List<Part> { new Part { Text = "Antworte bitte immer auf Deutsch, außer es wird explizit nach einer anderen Sprache gefragt." } }
                });
            }

            foreach (var historyMessage in chatHistory)
            {
                request.Contents.Add(new Content
                {
                    Role = historyMessage.Role == "user" ? "user" : "model",
                    Parts = new List<Part> { new Part { Text = historyMessage.Content } }
                });
            }

            request.Contents.Add(new Content
            {
                Role = "user",
                Parts = new List<Part> { new Part { Text = message } }
            });

            request.GenerationConfig = new GenerationConfig
            {
                Temperature = 0.7,
                TopK = 40,
                TopP = 0.95,
                MaxOutputTokens = 2048
            };

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var model = _configuration["GeminiAPI:Model"] ?? "gemini-2.0-flash-exp";
            var url = $"{_baseUrl}/models/{model}:generateContent?key={_apiKey}";

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"API-Fehler: {response.StatusCode} - {responseContent}");
                    var userFriendlyMessage = GetUserFriendlyErrorMessage(response.StatusCode, responseContent);
                    throw new HttpRequestException(userFriendlyMessage);
                }

                var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text != null)
                {
                    var responseText = geminiResponse.Candidates.First().Content.Parts.First().Text;
                    return responseText;
                }

                if (geminiResponse?.Error != null)
                {
                    _logger.LogError($"Gemini API Fehler: {geminiResponse.Error.Message}");
                    throw new Exception($"Gemini API Fehler: {geminiResponse.Error.Message}");
                }

                throw new Exception("Keine Antwort von Gemini erhalten");
            }
            catch (Exception ex)
            {
                _logger.LogError("Fehler beim Senden der Nachricht an Gemini", ex);
                throw;
            }
        }

        public async IAsyncEnumerable<string> SendMessageStreamAsync(string message, List<ChatMessage> chatHistory)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogError("API-Schlüssel nicht gesetzt");
                throw new InvalidOperationException("API-Schlüssel nicht gesetzt");
            }

            var fullResponse = await SendMessageAsync(message, chatHistory);
            
            var words = fullResponse.Split(' ');
            var currentChunk = "";
            
            foreach (var word in words)
            {
                currentChunk += word + " ";
                yield return currentChunk;
                currentChunk = "";
                await Task.Delay(20);
            }
            
            if (!string.IsNullOrEmpty(currentChunk.Trim()))
            {
                yield return currentChunk;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            if (string.IsNullOrEmpty(_apiKey))
                return false;

            try
            {
                var testMessage = "Hallo";
                var result = await SendMessageAsync(testMessage, new List<ChatMessage>());
                return !string.IsNullOrEmpty(result);
            }
            catch
            {
                return false;
            }
        }

        private string GetUserFriendlyErrorMessage(System.Net.HttpStatusCode statusCode, string responseContent)
        {
            switch (statusCode)
            {
                case System.Net.HttpStatusCode.BadRequest:
                    return "Ungültige Anfrage. Bitte überprüfe deine Eingabe.";
                
                case System.Net.HttpStatusCode.Unauthorized:
                    return "API-Schlüssel ungültig. Bitte überprüfe deine Einstellungen.";
                
                case System.Net.HttpStatusCode.Forbidden:
                    return "Zugriff verweigert. API-Berechtigung fehlt.";
                
                case System.Net.HttpStatusCode.NotFound:
                    return "API-Endpunkt nicht gefunden.";
                
                case System.Net.HttpStatusCode.TooManyRequests:
                    return "Tägliches Limit erreicht. Versuche es morgen wieder.";
                
                case System.Net.HttpStatusCode.InternalServerError:
                    return "Server-Fehler. Versuche es später nochmal.";
                
                case System.Net.HttpStatusCode.BadGateway:
                    return "Server-Antwort ungültig. Versuche es später nochmal.";
                
                case System.Net.HttpStatusCode.ServiceUnavailable:
                    return "Service nicht verfügbar. Versuche es später nochmal.";
                
                case System.Net.HttpStatusCode.GatewayTimeout:
                    return "Zeitüberschreitung. Versuche es später nochmal.";
                
                default:
                    return $"Verbindungsfehler. Versuche es später nochmal.";
            }
        }

    }
}