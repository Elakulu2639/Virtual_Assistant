using System.Collections.Generic;
using System.Threading.Tasks;
using ChatBot.Server.Models;

namespace ChatBot.Server.Services
{
    public interface IIntentService
    {
        Task<(string intent, List<string> entities, double? confidence)> AnalyzeIntentAsync(string userMessage, List<ChatHistory> chatHistory);
        Task<string> ClassifyIntentAsync(string userMessage);
    }
} 