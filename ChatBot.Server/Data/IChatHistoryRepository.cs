 using System.Collections.Generic;
using System.Threading.Tasks;
using ChatBot.Server.Models;

namespace ChatBot.Server.Data
{
    public interface IChatHistoryRepository
    {
        Task<List<ChatHistory>> GetChatHistoryBySessionAsync(string sessionId);
        Task SaveChatHistoryAsync(ChatHistory chatEntry);
    }
}
