using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChatBot.Server.Services
{
    public interface ILLMService
    {
        Task<string> GetLLMResponseAsync(List<object> messages, string model, double temperature, int maxTokens, double topP, double presencePenalty, double frequencyPenalty);
    }
} 