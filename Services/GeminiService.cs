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
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILoggerService _logger;
        private string? _apiKey;
        private readonly string _baseUrl;

        public GeminiService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILoggerService logger)
        {
            _httpClientFactory = httpClientFactory;
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
                _logger.LogError("API key not set");
                throw new InvalidOperationException("API key not set");
            }

            // Retry logic for transient failures
            int maxRetries = 2;
            int retryDelayMs = 1000;
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    return await SendMessageInternalAsync(message, chatHistory).ConfigureAwait(false);
                }
                catch (HttpRequestException ex) when (attempt < maxRetries && ex.Message.Contains("Service unavailable"))
                {
                    await Task.Delay(retryDelayMs).ConfigureAwait(false);
                }
            }
            
            // This should never be reached, but needed for compiler
            throw new HttpRequestException("All retry attempts failed");
        }

        private async Task<string> SendMessageInternalAsync(string message, List<ChatMessage> chatHistory)
        {

            var request = new GeminiRequest
            {
                Contents = new List<Content>()
            };

            if (chatHistory.Count == 0)
            {
                request.Contents.Add(new Content
                {
                    Role = "user",
                    Parts = new List<Part> { new Part { Text = "Please always respond in English, unless explicitly asked for another language." } }
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
                // Create HttpClient with timeout from factory
                using var httpClient = _httpClientFactory.CreateClient("Gemini");
                
                // Create a request message to add custom headers
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
                requestMessage.Content = content;
                
                // Note: Gemini API requires API key as query parameter
                // Moving it to header would break the API call
                // Keeping it as is for functionality, but logging is secure
                
                var response = await httpClient.SendAsync(requestMessage).ConfigureAwait(false);
                var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    // Don't log full response content to avoid potential sensitive data exposure
                    _logger.LogError($"API error: {response.StatusCode}");
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
                    _logger.LogError($"Gemini API error: {geminiResponse.Error.Message}");
                    throw new Exception($"Gemini API error: {geminiResponse.Error.Message}");
                }

                throw new Exception("No response received from Gemini");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error sending message to Gemini", ex);
                throw;
            }
        }

        public async IAsyncEnumerable<string> SendMessageStreamAsync(string message, List<ChatMessage> chatHistory)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogError("API key not set");
                yield return "Error: API key not set. Please configure your API key in settings.";
                yield break;
            }

            string fullResponse = null;
            string errorMessage = null;
            
            try
            {
                fullResponse = await SendMessageAsync(message, chatHistory).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in SendMessageAsync: {ex.Message}", ex);
                errorMessage = $"Error: {ex.Message}";
            }
            
            if (!string.IsNullOrEmpty(errorMessage))
            {
                yield return errorMessage;
                yield break;
            }
            
            if (string.IsNullOrEmpty(fullResponse))
            {
                _logger.LogError("Received empty response from Gemini API");
                yield return "Error: Failed to get response from Gemini API";
                yield break;
            }
            
            var words = fullResponse.Split(' ');
            var wordsPerChunk = 3; // Show 3 words at a time for faster display
            var accumulatedText = "";
            
            for (int i = 0; i < words.Length; i += wordsPerChunk)
            {
                var currentChunk = "";
                for (int j = 0; j < wordsPerChunk && i + j < words.Length; j++)
                {
                    currentChunk += words[i + j] + " ";
                }
                accumulatedText += currentChunk;
                yield return accumulatedText;
                await Task.Delay(5).ConfigureAwait(false); // Faster display (was 20ms)
            }
            
            if (!string.IsNullOrEmpty(accumulatedText.Trim()))
            {
                yield return accumulatedText;
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
                    return "Invalid request. Please check your input.";
                
                case System.Net.HttpStatusCode.Unauthorized:
                    return "API key invalid. Please check your settings.";
                
                case System.Net.HttpStatusCode.Forbidden:
                    return "Access denied. API permission missing.";
                
                case System.Net.HttpStatusCode.NotFound:
                    return "API endpoint not found.";
                
                case System.Net.HttpStatusCode.TooManyRequests:
                    return "Daily limit reached. Please try again tomorrow.";
                
                case System.Net.HttpStatusCode.InternalServerError:
                    return "Server error. Please try again later.";
                
                case System.Net.HttpStatusCode.BadGateway:
                    return "Invalid server response. Please try again later.";
                
                case System.Net.HttpStatusCode.ServiceUnavailable:
                    return "Service unavailable. Please try again later.";
                
                case System.Net.HttpStatusCode.GatewayTimeout:
                    return "Request timeout. Please try again later.";
                
                default:
                    return $"Connection error. Please try again later.";
            }
        }

    }
}