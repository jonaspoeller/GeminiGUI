using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeminiGUI.Models
{
    public class Chat
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public int MessageCount { get; set; }
        
        public long TotalTokens { get; set; }
        
        [NotMapped]
        public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
}

