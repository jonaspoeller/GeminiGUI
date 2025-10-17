using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GeminiGUI.Models
{
    public class GeminiRequest
    {
        [JsonPropertyName("contents")]
        public List<Content> Contents { get; set; } = new();

        [JsonPropertyName("generationConfig")]
        public GenerationConfig? GenerationConfig { get; set; }

        [JsonPropertyName("safetySettings")]
        public List<SafetySetting>? SafetySettings { get; set; }
    }

    public class Content
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("parts")]
        public List<Part> Parts { get; set; } = new();
    }

    public class Part
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    public class GenerationConfig
    {
        [JsonPropertyName("temperature")]
        public double? Temperature { get; set; } = 0.7;

        [JsonPropertyName("topK")]
        public int? TopK { get; set; } = 40;

        [JsonPropertyName("topP")]
        public double? TopP { get; set; } = 0.95;

        [JsonPropertyName("maxOutputTokens")]
        public int? MaxOutputTokens { get; set; } = 2048;
    }

    public class SafetySetting
    {
        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("threshold")]
        public string Threshold { get; set; } = "BLOCK_MEDIUM_AND_ABOVE";
    }
}