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
    public class IntentService : IIntentService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<IntentService> _logger;
        private readonly string _pythonNlpUrl = "http://localhost:8000/analyze";

        public IntentService(HttpClient httpClient, ILogger<IntentService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<(string intent, List<string> entities, double? confidence)> AnalyzeIntentAsync(string userMessage, List<ChatHistory> chatHistory)
        {
            string sessionId = chatHistory?.FirstOrDefault()?.SessionId ?? Guid.NewGuid().ToString();
            string prevBotResponse = chatHistory?.OrderBy(h => h.Timestamp).LastOrDefault()?.BotResponse ?? string.Empty;
            try
            {
                var payload = new {
                    text = userMessage,
                    session_id = sessionId,
                    prev_bot_response = prevBotResponse
                };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_pythonNlpUrl, content);
                if (!response.IsSuccessStatusCode)
                    return ("general_query", new List<string>(), 0.9);
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                var intent = doc.RootElement.TryGetProperty("intent", out var intentProp) ? intentProp.GetString() : null;
                var entities = new List<string>();
                if (doc.RootElement.TryGetProperty("entities", out var entitiesProp) && entitiesProp.ValueKind == JsonValueKind.Object)
                {
                    foreach (var ent in entitiesProp.EnumerateObject())
                    {
                        entities.Add(ent.Value.GetString() ?? string.Empty);
                    }
                }
                double? confidence = null;
                if (doc.RootElement.TryGetProperty("confidence", out var confProp) && confProp.ValueKind == JsonValueKind.Number)
                {
                    confidence = confProp.GetDouble();
                }
                _logger.LogInformation("Analysis result - Intent: {Intent}, Confidence: {Confidence}", intent, confidence);
                return (intent ?? "general_query", entities, confidence ?? 0.9);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing intent with Python NLP");
                return ("general_query", new List<string>(), 0.9);
            }
        }

        public async Task<string> ClassifyIntentAsync(string userMessage)
        {
            var payload = new { text = userMessage };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("http://localhost:8000/classify_intent", content);
            if (!response.IsSuccessStatusCode)
                return null;
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("intent", out var intentProp) ? intentProp.GetString() : null;
        }
    }
} 