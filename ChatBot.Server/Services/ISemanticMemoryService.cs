using System.Collections.Generic;
using System.Threading.Tasks;
using ChatBot.Server.Models;

namespace ChatBot.Server.Services
{
    public interface ISemanticMemoryService
    {
        Task StoreMessageAsync(string sessionId, string message, string role);
        Task<List<ChatHistory>> GetRelevantHistoryAsync(string userMessage, List<ChatHistory> fullChatHistory);
    }
} 