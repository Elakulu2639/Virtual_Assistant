using System.Collections.Generic;
using System.Threading.Tasks;
using ChatBot.Server.Models;

namespace ChatBot.Server.Services
{
    public interface INlpService
    {
        Task<(string intent, List<string> entities, double? confidence)> AnalyzeIntentAsync(string userMessage, List<ChatHistory> chatHistory);
        Task<List<string>> ExtractEntitiesAsync(string userMessage);
        Task<List<ChatHistory>> GetRelevantChatHistory(string userMessage, List<ChatHistory> fullChatHistory);
        Task<string> ClassifyIntentAsync(string userMessage);
        Task<Dictionary<string, string>> ExtractEntitiesFromPythonAsync(string userMessage);
        Task StoreMessageInChromaAsync(string sessionId, string message, string role);
    }
} 