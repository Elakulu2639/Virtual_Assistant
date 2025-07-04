using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ChatBot.Server.Models;
using Microsoft.Extensions.Logging;

namespace ChatBot.Server.Services
{
    public class PythonNlpService : IPythonNlpService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PythonNlpService> _logger;
        private readonly string _analyzeUrl = "http://localhost:8000/analyze";

        public PythonNlpService(HttpClient httpClient, ILogger<PythonNlpService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<JsonElement?> AnalyzeAsync(string userMessage, List<ChatHistory> chatHistory, string prevBotResponse)
        {
            try
            {
                var historyTexts = chatHistory?.OrderBy(h => h.Timestamp).Select(h => h.UserMessage).ToList() ?? new List<string>();
                var payload = new { text = userMessage, history = historyTexts, prev_bot_response = prevBotResponse };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_analyzeUrl, content);
                if (!response.IsSuccessStatusCode)
                    return null;
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                return doc.RootElement;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Python NLP service: {Message}", ex.Message);
                return null;
            }
        }
    }
} 