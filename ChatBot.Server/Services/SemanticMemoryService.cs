using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ChatBot.Server.Models;
using Microsoft.Extensions.Logging;

namespace ChatBot.Server.Services
{
    public class SemanticMemoryService : ISemanticMemoryService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SemanticMemoryService> _logger;
        private readonly string _storeMessageUrl = "http://localhost:8000/store_message";
        private readonly string _getRelevantHistoryUrl = "http://localhost:8000/get_relevant_history";

        public SemanticMemoryService(HttpClient httpClient, ILogger<SemanticMemoryService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task StoreMessageAsync(string sessionId, string message, string role)
        {
            try
            {
                var payload = new { session_id = sessionId, message = message, role = role };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_storeMessageUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to store message in ChromaDB: {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing message in ChromaDB");
            }
        }

        public async Task<List<ChatHistory>> GetRelevantHistoryAsync(string userMessage, List<ChatHistory> fullChatHistory)
        {
            if (fullChatHistory == null || !fullChatHistory.Any())
                return new List<ChatHistory>();

            string sessionId = fullChatHistory.First().SessionId;
            try
            {
                var url = $"{_getRelevantHistoryUrl}?query={Uri.EscapeDataString(userMessage)}&session_id={sessionId}&top_k=5";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return new List<ChatHistory>();
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("relevant_history", out var historyProp))
                {
                    var chatHistoryResults = new List<ChatHistory>();
                    foreach (var item in historyProp.EnumerateArray())
                    {
                        string message = null, role = null, timestamp = null;
                        foreach (var prop in item.EnumerateObject())
                        {
                            switch (prop.Name)
                            {
                                case "message": message = prop.Value.GetString(); break;
                                case "role": role = prop.Value.GetString(); break;
                                case "timestamp": timestamp = prop.Value.GetString(); break;
                            }
                        }
                        if (message != null && role != null && timestamp != null)
                        {
                            var chatEntry = new ChatHistory
                            {
                                SessionId = sessionId,
                                UserMessage = role == "user" ? message : "",
                                BotResponse = role == "bot" ? message : "",
                                Timestamp = DateTime.Parse(timestamp)
                            };
                            chatHistoryResults.Add(chatEntry);
                        }
                    }
                    return chatHistoryResults;
                }
                return new List<ChatHistory>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting relevant history from ChromaDB");
                return new List<ChatHistory>();
            }
        }
    }
} 