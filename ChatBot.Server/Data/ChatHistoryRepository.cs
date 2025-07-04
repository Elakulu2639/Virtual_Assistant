using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChatBot.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatBot.Server.Data
{
    public class ChatHistoryRepository : IChatHistoryRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public ChatHistoryRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<ChatHistory>> GetChatHistoryBySessionAsync(string sessionId)
        {
            return await _dbContext.ChatHistories
                .Where(h => h.SessionId == sessionId)
                .OrderBy(h => h.Timestamp)
                .ToListAsync();
        }

        public async Task SaveChatHistoryAsync(ChatHistory chatEntry)
        {
            _dbContext.ChatHistories.Add(chatEntry);
            await _dbContext.SaveChangesAsync();
        }
    }
} 