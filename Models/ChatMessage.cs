using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeminiGUI.Models
{
    public class ChatMessage
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int ChatId { get; set; }
        
        [Required]
        public string Role { get; set; } = string.Empty; // "user" or "model"
        
        [Required]
        public string Content { get; set; } = string.Empty;
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        public int TokenCount { get; set; }
        
        [ForeignKey("ChatId")]
        public virtual Chat Chat { get; set; } = null!;
    }
}

