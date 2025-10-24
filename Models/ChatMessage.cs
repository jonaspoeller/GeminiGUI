using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;

namespace GeminiGUI.Models
{
    public class ChatMessage : INotifyPropertyChanged
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int ChatId { get; set; }
        
        [Required]
        public string Role { get; set; } = string.Empty; // "user" or "model"
        
        // Display name for UI - no converter needed!
        [NotMapped]
        public string DisplayName => Role == "user" ? "You" : "Gemini";
        
        private string _content = string.Empty;
        [Required]
        public string Content 
        { 
            get => _content;
            set
            {
                if (_content != value)
                {
                    _content = value;
                    OnPropertyChanged(nameof(Content));
                }
            }
        }
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        public int TokenCount { get; set; }
        
        [ForeignKey("ChatId")]
        public virtual Chat Chat { get; set; } = null!;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

