using System.Collections.Generic;
using System.Threading.Tasks;
using ChatBot.Server.Models;
using System.Linq; // Added for LINQ operations
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ChatBot.Server.Settings;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace ChatBot.Server.Services
{
    public class NlpService : INlpService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NlpService> _logger;
        private readonly string _pythonNlpUrl = "http://localhost:8000/analyze";
        private readonly string _storeMessageUrl = "http://localhost:8000/store_message";
        private readonly string _getRelevantHistoryUrl = "http://localhost:8000/get_relevant_history";
        private readonly string _changeDomainUrl = "http://localhost:8000/change_domain";
        private readonly string _getDomainsUrl = "http://localhost:8000/get_domains";

        public NlpService(HttpClient httpClient, ILogger<NlpService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<(string intent, List<string> entities, double? confidence)> AnalyzeIntentAsync(string userMessage, List<ChatHistory> chatHistory)
        {
            // Get session ID from chat history if available
            string sessionId = chatHistory?.FirstOrDefault()?.SessionId ?? Guid.NewGuid().ToString();
            string prevBotResponse = chatHistory?.OrderBy(h => h.Timestamp).LastOrDefault()?.BotResponse ?? string.Empty;
            
            var (pyIntent, pyEntities, contextUsed, domain, confidence) = await AnalyzeWithPythonNlpAsync(userMessage, sessionId, prevBotResponse);
            return (pyIntent ?? "general_query", pyEntities?.Values.ToList() ?? new List<string>(), confidence ?? 0.9);
        }

        public async Task<List<string>> ExtractEntitiesAsync(string userMessage, List<ChatHistory> chatHistory = null)
        {
            string sessionId = chatHistory?.FirstOrDefault()?.SessionId ?? Guid.NewGuid().ToString();
            string prevBotResponse = chatHistory?.OrderBy(h => h.Timestamp).LastOrDefault()?.BotResponse ?? string.Empty;
            var (_, pyEntities, _, _, _) = await AnalyzeWithPythonNlpAsync(userMessage, sessionId, prevBotResponse);
            return pyEntities?.Values.ToList() ?? new List<string>();
        }

        public async Task<List<string>> ExtractEntitiesAsync(string userMessage)
        {
            return await ExtractEntitiesAsync(userMessage, null);
        }

        public async Task<List<ChatHistory>> GetRelevantChatHistory(string userMessage, List<ChatHistory> fullChatHistory)
        {
            // Use ChromaDB for semantic retrieval instead of fuzzy matching
            if (fullChatHistory == null || !fullChatHistory.Any())
                return new List<ChatHistory>();

            string sessionId = fullChatHistory.First().SessionId;
            
            try
            {
                var relevantHistory = await GetRelevantHistoryFromChromaAsync(userMessage, sessionId);
                
                // Convert ChromaDB results back to ChatHistory format for compatibility
                var chatHistoryResults = new List<ChatHistory>();
                foreach (var item in relevantHistory)
                {
                    if (item.TryGetValue("message", out var message) && 
                        item.TryGetValue("role", out var role) &&
                        item.TryGetValue("timestamp", out var timestamp))
                    {
                        var chatEntry = new ChatHistory
                        {
                            SessionId = sessionId,
                            UserMessage = role.ToString() == "user" ? message.ToString() : "",
                            BotResponse = role.ToString() == "bot" ? message.ToString() : "",
                            Timestamp = DateTime.Parse(timestamp.ToString())
                        };
                        chatHistoryResults.Add(chatEntry);
                    }
                }
                
                return chatHistoryResults;
            }
            catch (Exception ex)
            {
                // Fallback to old method if ChromaDB fails
                _logger.LogWarning(ex, "ChromaDB retrieval failed, falling back to fuzzy matching");
                return GetRelevantChatHistoryFallback(userMessage, fullChatHistory);
            }
        }

        private List<ChatHistory> GetRelevantChatHistoryFallback(string userMessage, List<ChatHistory> fullChatHistory)
        {
            // Keep the old fuzzy matching logic as fallback
            var relevantHistory = new List<Tuple<ChatHistory, double>>();
            var cleanUserMessage = userMessage.ToLowerInvariant();

            foreach (var historyEntry in fullChatHistory)
            {
                var userMessageScore = CalculateSimilarity(cleanUserMessage, historyEntry.UserMessage.ToLowerInvariant());
                var botResponseScore = CalculateSimilarity(cleanUserMessage, historyEntry.BotResponse.ToLowerInvariant());

                // Take the higher of the two scores for relevance
                var maxScore = Math.Max(userMessageScore, botResponseScore);

                if (maxScore >= 0.6) // Threshold for relevance
                {
                    relevantHistory.Add(Tuple.Create(historyEntry, maxScore));
                }
            }

            // Order by score descending and take the top N entries
            return relevantHistory.OrderByDescending(h => h.Item2).Take(5).Select(h => h.Item1).ToList();
        }

        private double CalculateSimilarity(string text1, string text2)
        {
            // Simple similarity calculation (you can replace this with more sophisticated algorithms)
            var words1 = text1.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var words2 = text2.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            
            var intersection = words1.Intersect(words2).Count();
            var union = words1.Union(words2).Count();
            
            return union > 0 ? (double)intersection / union : 0;
        }

        private async Task<List<Dictionary<string, object>>> GetRelevantHistoryFromChromaAsync(string query, string sessionId)
        {
            try
            {
                var url = $"{_getRelevantHistoryUrl}?query={Uri.EscapeDataString(query)}&session_id={sessionId}&top_k=5";
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                    return new List<Dictionary<string, object>>();
                
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                
                if (doc.RootElement.TryGetProperty("relevant_history", out var historyProp))
                {
                    var historyList = new List<Dictionary<string, object>>();
                    foreach (var item in historyProp.EnumerateArray())
                    {
                        var dict = new Dictionary<string, object>();
                        foreach (var prop in item.EnumerateObject())
                        {
                            // Handle different JSON value types properly
                            switch (prop.Value.ValueKind)
                            {
                                case JsonValueKind.String:
                                    dict[prop.Name] = prop.Value.GetString() ?? "";
                                    break;
                                case JsonValueKind.Number:
                                    dict[prop.Name] = prop.Value.GetDouble();
                                    break;
                                case JsonValueKind.True:
                                case JsonValueKind.False:
                                    dict[prop.Name] = prop.Value.GetBoolean();
                                    break;
                                default:
                                    dict[prop.Name] = prop.Value.ToString();
                                    break;
                            }
                        }
                        historyList.Add(dict);
                    }
                    return historyList;
                }
                
                return new List<Dictionary<string, object>>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting relevant history from ChromaDB");
                return new List<Dictionary<string, object>>();
            }
        }

        public async Task StoreMessageInChromaAsync(string sessionId, string message, string role)
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

        private async Task<(string? intent, Dictionary<string, string>? entities, string? contextUsed, string? domain, double? confidence)> AnalyzeWithPythonNlpAsync(string userMessage, string sessionId, string prevBotResponse)
        {
            try
            {
                // Retrieve last user message from ChromaDB/session for context
                string lastUserMessage = null;
                var sessionHistory = await GetRelevantHistoryFromChromaAsync("", sessionId);
                if (sessionHistory != null && sessionHistory.Count > 0)
                {
                    foreach (var msg in sessionHistory)
                    {
                        if (msg.TryGetValue("role", out var role) && role.ToString() == "user")
                        {
                            lastUserMessage = msg["message"].ToString();
                            break;
                        }
                    }
                }

                var payload = new {
                    text = userMessage,
                    session_id = sessionId,
                    prev_bot_response = prevBotResponse,
                    last_user_message = lastUserMessage
                };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_pythonNlpUrl, content);
                
                if (!response.IsSuccessStatusCode)
                    return (null, null, null, null, null);
                
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);

                // Optionally handle rewritten message (for future extensibility)
                if (doc.RootElement.TryGetProperty("rewritten", out var rewrittenProp))
                {
                    var rewritten = rewrittenProp.GetString();
                    if (!string.IsNullOrEmpty(rewritten) && rewritten != userMessage)
                    {
                        userMessage = rewritten;
                    }
                }
                
                var intent = doc.RootElement.TryGetProperty("intent", out var intentProp) ? intentProp.GetString() : null;
                var entities = new Dictionary<string, string>();
                
                if (doc.RootElement.TryGetProperty("entities", out var entitiesProp) && entitiesProp.ValueKind == JsonValueKind.Object)
                {
                    foreach (var ent in entitiesProp.EnumerateObject())
                    {
                        entities[ent.Name] = ent.Value.GetString() ?? string.Empty;
                    }
                }
                
                var contextUsed = doc.RootElement.TryGetProperty("context_used", out var contextProp) ? contextProp.GetString() : null;
                var domain = doc.RootElement.TryGetProperty("domain", out var domainProp) ? domainProp.GetString() : null;
                
                // Fix the confidence parsing to handle nullable values properly
                double? confidence = null;
                if (doc.RootElement.TryGetProperty("confidence", out var confProp))
                {
                    if (confProp.ValueKind == JsonValueKind.Number)
                    {
                        confidence = confProp.GetDouble();
                    }
                }

                _logger.LogInformation("Analysis result - Domain: {Domain}, Intent: {Intent}, Confidence: {Confidence}", 
                    domain, intent, confidence);

                return (intent, entities, contextUsed, domain, confidence);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing with Python NLP");
                return (null, null, null, null, null);
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

        public async Task<Dictionary<string, string>> ExtractEntitiesFromPythonAsync(string userMessage)
        {
            var payload = new { text = userMessage };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("http://localhost:8000/extract_entities", content);
            if (!response.IsSuccessStatusCode)
                return new Dictionary<string, string>();
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var entities = new Dictionary<string, string>();
            if (doc.RootElement.TryGetProperty("entities", out var entitiesProp) && entitiesProp.ValueKind == JsonValueKind.Object)
            {
                foreach (var ent in entitiesProp.EnumerateObject())
                {
                    entities[ent.Name] = ent.Value.GetString() ?? string.Empty;
                }
            }
            return entities;
        }

        // New methods for domain management
        public async Task<bool> ChangeDomainAsync(string domain)
        {
            try
            {
                var payload = new { domain = domain };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_changeDomainUrl, content);
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var doc = JsonDocument.Parse(json);
                    var status = doc.RootElement.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : "";
                    return status == "domain_changed";
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing domain");
                return false;
            }
        }

        public async Task<Dictionary<string, object>> GetAvailableDomainsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(_getDomainsUrl);
                
                if (!response.IsSuccessStatusCode)
                    return new Dictionary<string, object>();

                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                var domains = new Dictionary<string, object>();

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    // Handle different JSON value types properly
                    switch (prop.Value.ValueKind)
                    {
                        case JsonValueKind.String:
                            domains[prop.Name] = prop.Value.GetString() ?? "";
                            break;
                        case JsonValueKind.Number:
                            domains[prop.Name] = prop.Value.GetDouble();
                            break;
                        case JsonValueKind.True:
                        case JsonValueKind.False:
                            domains[prop.Name] = prop.Value.GetBoolean();
                            break;
                        case JsonValueKind.Array:
                            var array = new List<object>();
                            foreach (var item in prop.Value.EnumerateArray())
                            {
                                array.Add(item.GetString() ?? "");
                            }
                            domains[prop.Name] = array;
                            break;
                        default:
                            domains[prop.Name] = prop.Value.ToString();
                            break;
                    }
                }

                return domains;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available domains");
                return new Dictionary<string, object>();
            }
        }
    }
} 