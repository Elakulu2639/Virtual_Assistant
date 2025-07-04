using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ChatBot.Server.Models;

namespace ChatBot.Server.Services
{
    public interface IPythonNlpService
    {
        Task<JsonElement?> AnalyzeAsync(string userMessage, List<ChatHistory> chatHistory, string prevBotResponse);
    }
} 