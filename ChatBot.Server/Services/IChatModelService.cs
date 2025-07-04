using System.Threading.Tasks;

namespace ChatBot.Server.Services
{
    public interface IChatModelService
    {
        Task<string> GetChatResponseAsync(string userMessage, string? sessionId);
    }
}
