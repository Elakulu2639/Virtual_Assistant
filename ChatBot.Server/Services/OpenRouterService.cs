using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ChatBot.Server.Data;
using ChatBot.Server.Models;
using ChatBot.Server.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace ChatBot.Server.Services
{
    public class OpenRouterService : IChatModelService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenRouterService> _logger;
        private readonly ApplicationDbContext _dbContext;
        private readonly INlpService _nlpService;
        private readonly IOptions<OpenRouterSettings> _settings;

        public OpenRouterService(
            HttpClient httpClient,
            ILogger<OpenRouterService> logger,
            ApplicationDbContext dbContext,
            INlpService nlpService,
            IOptions<OpenRouterSettings> settings)
        {
            _httpClient = httpClient;
            _logger = logger;
            _dbContext = dbContext;
            _nlpService = nlpService;
            _settings = settings;

            var apiKey = settings.Value.ApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentNullException(nameof(apiKey), "OpenRouter API key is not configured");
            }

            _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "http://localhost:62963");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "ERP Assistant");
        }

        public async Task<string> GetChatResponseAsync(string userMessage, string? sessionId)
        {
            try
            {
                _logger.LogInformation("Processing message: {Message} for session: {SessionId}", userMessage, sessionId);

                var fullChatHistory = new List<ChatHistory>();
                if (!string.IsNullOrEmpty(sessionId))
                {
                    fullChatHistory = await _dbContext.ChatHistories
                        .Where(h => h.SessionId == sessionId)
                        .OrderByDescending(h => h.Timestamp)
                        .ToListAsync();
                    fullChatHistory.Reverse(); 
                }

                // Get previous bot response for context
                string prevBotResponse = fullChatHistory.LastOrDefault()?.BotResponse ?? string.Empty;

                // Step 1: Store user message in ChromaDB for semantic memory
                if (!string.IsNullOrEmpty(sessionId))
                {
                    await _nlpService.StoreMessageInChromaAsync(sessionId, userMessage, "user");
                }

                // Step 2: Classify intent using fine-tuned model
                var classifiedIntent = await _nlpService.ClassifyIntentAsync(userMessage);
                _logger.LogInformation("Classified intent: {Intent}", classifiedIntent);

                // Step 3: Call Python NLP for semantic search and context-aware answer
                var nlpResult = await CallPythonNlpService(userMessage, fullChatHistory, prevBotResponse);
                if (nlpResult.HasValue && nlpResult.Value.ValueKind == JsonValueKind.Object)
                {
                    var root = nlpResult.Value;
                    if (root.TryGetProperty("source", out var sourceProp) && sourceProp.GetString() == "csv")
                    {
                        var csvAnswer = root.TryGetProperty("answer", out var answerProp) ? answerProp.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(csvAnswer))
                        {
                            // Conversational rephrasing using the AI model
                            var rephrasePrompt = $@"
You are an intelligent ERP assistant. 
Rephrase the following answer in a friendly, conversational way for the user, but do not add any information not present in the answer. 
If the answer is unclear, clarify only using the information provided. 
User question: {userMessage}
ERP answer: {csvAnswer}
";
                            var rephraseMessages = new List<object>
            {
                                new { role = "system", content = rephrasePrompt },
                                new { role = "user", content = userMessage }
                            };

                            var rephraseJsonPayload = JsonSerializer.Serialize(new
                            {
                                model = "deepseek/deepseek-r1-0528-qwen3-8b:free",
                                messages = rephraseMessages,
                                temperature = 0.5,
                                max_tokens = 500,
                                top_p = 0.8,
                                presence_penalty = 0.6,
                                frequency_penalty = 0.3
                            });

                            var rephraseContent = new StringContent(rephraseJsonPayload, Encoding.UTF8, "application/json");
                            var rephraseResponse = await _httpClient.PostAsync("chat/completions", rephraseContent);
                            var rephraseResponseContent = await rephraseResponse.Content.ReadAsStringAsync();

                            var conversationalAnswer = ExtractResponseFromJson(rephraseResponseContent);

                            // Store bot response in ChromaDB
                            if (!string.IsNullOrEmpty(sessionId))
                            {
                                await _nlpService.StoreMessageInChromaAsync(sessionId, conversationalAnswer, "bot");
                            }

                            await SaveChatHistory(userMessage, conversationalAnswer, "csv_match", new List<string>(), 1.0, sessionId);
                            return conversationalAnswer;
                        }
                }
                }

                // Step 4: Use ChromaDB for semantic context retrieval instead of fixed context window
                var relevantHistoricalMessages = await _nlpService.GetRelevantChatHistory(userMessage, fullChatHistory);
                _logger.LogInformation("Retrieved {Count} relevant messages from semantic memory", relevantHistoricalMessages.Count);

                // Step 5: Use LLM with semantically relevant context
                var historicalMessages = new List<object>();
                foreach (var turn in relevantHistoricalMessages)
                {
                    historicalMessages.Add(new { role = "user", content = turn.UserMessage });
                    historicalMessages.Add(new { role = "assistant", content = turn.BotResponse });
                }

                var systemPromptBuilder = new StringBuilder();
                systemPromptBuilder.AppendLine("You are an intelligent ERP assistant. You must ONLY answer questions related to company ERP, HR, business processes, organizational tasks, sales, project management, or related company workflows. If a user asks a follow-up question, use the previous conversation context to clarify and answer if it is ERP-related. Only refuse if the question is clearly not related to ERP, HR, or business processes. If a user asks a question that is not related to these topics, politely refuse to answer and say: 'I'm sorry, I can only assist with company ERP-related questions and processes.' Do NOT answer questions about general knowledge, unrelated topics, or personal matters. Be strict in this policy.");
                systemPromptBuilder.AppendLine();
                systemPromptBuilder.AppendLine("You have access to information regarding:");
                systemPromptBuilder.AppendLine("- HR policies including attendance, leave, conduct, and performance evaluation");
                systemPromptBuilder.AppendLine("- Business processes and workflows");
                systemPromptBuilder.AppendLine("- Organizational data and procedures");
                systemPromptBuilder.AppendLine("- Sales and marketing information");
                systemPromptBuilder.AppendLine("- Project management details");
                systemPromptBuilder.AppendLine("- Customer service guidelines");
                systemPromptBuilder.AppendLine("- Compliance requirements");
                systemPromptBuilder.AppendLine("- Training materials");
                systemPromptBuilder.AppendLine("Guidelines for responses:");
                systemPromptBuilder.AppendLine("1. For general greetings or casual questions:");
                systemPromptBuilder.AppendLine("   - Respond naturally and briefly");
                systemPromptBuilder.AppendLine("   - Be friendly but professional");
                systemPromptBuilder.AppendLine("   - Offer to help with specific tasks");
                systemPromptBuilder.AppendLine("   - Mention key areas you can assist with (HR, Sales, Finance, etc.)");
                systemPromptBuilder.AppendLine("2. For specific questions:");
                systemPromptBuilder.AppendLine("   - Provide helpful and accurate information");
                systemPromptBuilder.AppendLine("   - Be clear and concise");
                systemPromptBuilder.AppendLine("   - If you're not sure about something, say so");
                systemPromptBuilder.AppendLine("   - Suggest relevant areas or departments that might help");
                systemPromptBuilder.AppendLine("   - If you cannot find a direct answer within your knowledge, state that and offer to help with other ERP-related questions.");
                systemPromptBuilder.AppendLine();
                systemPromptBuilder.AppendLine("**Important:** Consider the current conversation history to maintain context and respond coherently. Do not repeat information already provided in previous turns unless explicitly asked.");

                var systemPrompt = systemPromptBuilder.ToString();
                var messages = new List<object>();
                messages.Add(new { role = "system", content = systemPrompt });
                messages.AddRange(historicalMessages);
                messages.Add(new { role = "user", content = userMessage });

                var jsonPayload = JsonSerializer.Serialize(new
                {
                    model = "deepseek/deepseek-r1-0528-qwen3-8b:free",
                    messages = messages,
                    temperature = 0.5,
                    max_tokens = 500,
                    top_p = 0.8,
                    presence_penalty = 0.6,
                    frequency_penalty = 0.3
                });

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("chat/completions", content);
                var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                    _logger.LogError("OpenRouter API error: {StatusCode}, Response: {ErrorContent}", 
                        response.StatusCode, 
                        responseContent);
                    throw new Exception($"API request failed with status code {response.StatusCode}");
                }

                var botResponse = ExtractResponseFromJson(responseContent);
                if (string.IsNullOrWhiteSpace(botResponse))
                {
                    throw new Exception("Empty response from API");
            }

                // Step 6: Store bot response in ChromaDB for semantic memory
                if (!string.IsNullOrEmpty(sessionId))
                {
                    await _nlpService.StoreMessageInChromaAsync(sessionId, botResponse, "bot");
                }

                // Step 7: Analyze intent and save history after getting the final bot response
                var (intent, entities, confidence) = await _nlpService.AnalyzeIntentAsync(userMessage, relevantHistoricalMessages);
                await SaveChatHistory(userMessage, botResponse, intent, entities, confidence, sessionId);

                return botResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat message: {Message}", ex.Message);
                throw; 
            }
        }

        private string ExtractResponseFromJson(string json)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("choices", out JsonElement choicesElement))
                    {
                        if (choicesElement.EnumerateArray().Any())
                        {
                            var firstChoice = choicesElement.EnumerateArray().First();
                            if (firstChoice.TryGetProperty("message", out JsonElement messageElement))
                            {
                                if (messageElement.TryGetProperty("content", out JsonElement contentElement))
                                {
                                    return contentElement.GetString();
                                }
                                else
                                {
                                    _logger.LogWarning("JSON parsing error: 'content' property not found in message.");
                                }
                            }
                            else
                            {
                                _logger.LogWarning("JSON parsing error: 'message' property not found in first choice.");
                            }
                        }
                        else
                        {
                            _logger.LogWarning("JSON parsing error: 'choices' array is empty.");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("JSON parsing error: 'choices' property not found in root element.");
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error: {Message}", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during JSON parsing: {Message}", ex.Message);
            }
            return null; // Return null if parsing fails
        }

        private async Task SaveChatHistory(string userMessage, string botResponse, string intent, List<string> entities, double? confidence, string? sessionId)
        {
            var chatEntry = new ChatHistory
            {
                SessionId = sessionId ?? Guid.NewGuid().ToString(),
                UserMessage = userMessage,
                BotResponse = botResponse,
                Timestamp = DateTime.UtcNow,
                Intent = intent,
                Confidence = confidence,
                Entities = entities != null ? string.Join(",", entities) : null
            };
            _dbContext.ChatHistories.Add(chatEntry);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Chat history saved for session {SessionId}", chatEntry.SessionId);
        }

        // Helper to call Python NLP service for semantic/context-aware answer
        private async Task<JsonElement?> CallPythonNlpService(string userMessage, List<ChatHistory> chatHistory, string prevBotResponse)
        {
            try
            {
                var httpClient = new HttpClient();
                var historyTexts = chatHistory?.OrderBy(h => h.Timestamp).Select(h => h.UserMessage).ToList() ?? new List<string>();
                var payload = new { text = userMessage, history = historyTexts, prev_bot_response = prevBotResponse };
                var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync("http://localhost:8000/analyze", content);
                if (!response.IsSuccessStatusCode)
                    return null;
                var json = await response.Content.ReadAsStringAsync();
                var doc = System.Text.Json.JsonDocument.Parse(json);
                return doc.RootElement;
            }
            catch
            {
                return null;
            }
        }
    }
}
