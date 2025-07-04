using System.ComponentModel.DataAnnotations;

namespace ChatBot.Server.Models
{
    public class ChatHistory
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string SessionId { get; set; }
        
        [Required]
        public string UserMessage { get; set; }
        
        [Required]
        public string BotResponse { get; set; }
        
        [Required]
        public DateTime Timestamp { get; set; }
        
        public string? Intent { get; set; }
        
        public string? Entities { get; set; }
        
        public double? Confidence { get; set; }
    }
} 