namespace ChatBot.Server.Models
{
    public class ChatInputDto
    {
        public string UserMessage { get; set; } = string.Empty;
        public string? SessionId { get; set; }
    }
} 