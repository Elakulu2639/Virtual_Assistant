using System.Threading.Tasks;
using ChatBot.Server.Models;
using System.Collections.Generic;
using System.Text;
using ChatBot.Server.Data;
using System;
using System.Linq;
using System.Text.Json;

namespace ChatBot.Server.Services
{
    public class ChatOrchestratorService : IChatModelService
    {
        private readonly ILLMService _llmService;
        private readonly ISemanticMemoryService _semanticMemoryService;
        private readonly IIntentService _intentService;
        private readonly IChatHistoryRepository _chatHistoryRepository;
        private readonly IPythonNlpService _pythonNlpService;

        public ChatOrchestratorService(
            ILLMService llmService,
            ISemanticMemoryService semanticMemoryService,
            IIntentService intentService,
            IChatHistoryRepository chatHistoryRepository,
            IPythonNlpService pythonNlpService)
        {
            _llmService = llmService;
            _semanticMemoryService = semanticMemoryService;
            _intentService = intentService;
            _chatHistoryRepository = chatHistoryRepository;
            _pythonNlpService = pythonNlpService;
        }

        public async Task<string> GetChatResponseAsync(string userMessage, string? sessionId)
        {
            var fullChatHistory = new List<ChatHistory>();
            if (!string.IsNullOrEmpty(sessionId))
            {
                fullChatHistory = await _chatHistoryRepository.GetChatHistoryBySessionAsync(sessionId);
            }

            // Step 1: Store user message in semantic memory
            if (!string.IsNullOrEmpty(sessionId))
            {
                await _semanticMemoryService.StoreMessageAsync(sessionId, userMessage, "user");
            }

            // Step 2: Classify intent
            var classifiedIntent = await _intentService.ClassifyIntentAsync(userMessage);

            // Step 3: Call Python NLP for direct answer
            string prevBotResponse = fullChatHistory.LastOrDefault()?.BotResponse ?? string.Empty;
            var nlpResult = await _pythonNlpService.AnalyzeAsync(userMessage, fullChatHistory, prevBotResponse);
            if (nlpResult.HasValue && nlpResult.Value.ValueKind == JsonValueKind.Object)
            {
                var root = nlpResult.Value;
                if (root.TryGetProperty("source", out var sourceProp) && sourceProp.GetString() == "csv")
                {
                    var csvAnswer = root.TryGetProperty("answer", out var answerProp) ? answerProp.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(csvAnswer))
                    {
                        // Step 3a: Rephrase with LLM
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
                        var conversationalAnswer = await _llmService.GetLLMResponseAsync(
                            rephraseMessages, "deepseek/deepseek-r1-0528-qwen3-8b:free", 0.5, 500, 0.8, 0.6, 0.3);

                        if (!string.IsNullOrEmpty(sessionId))
                        {
                            await _semanticMemoryService.StoreMessageAsync(sessionId, conversationalAnswer, "bot");
                        }
                        // Save chat history
                        var csvChatEntry = new ChatHistory
                        {
                            SessionId = sessionId ?? Guid.NewGuid().ToString(),
                            UserMessage = userMessage,
                            BotResponse = conversationalAnswer,
                            Timestamp = DateTime.UtcNow,
                            Intent = "csv_match",
                            Entities = string.Empty,
                            Confidence = 1.0
                        };
                        await _chatHistoryRepository.SaveChatHistoryAsync(csvChatEntry);
                        return conversationalAnswer;
                    }
                }
            }

            // Step 4: Use semantic memory for context
            var relevantHistoricalMessages = await _semanticMemoryService.GetRelevantHistoryAsync(userMessage, fullChatHistory);

            // Step 5: Use LLM with context
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

            var botResponse = await _llmService.GetLLMResponseAsync(
                messages, "deepseek/deepseek-r1-0528-qwen3-8b:free", 0.5, 500, 0.8, 0.6, 0.3);

            if (string.IsNullOrWhiteSpace(botResponse))
            {
                throw new System.Exception("Empty response from LLM");
            }

            if (!string.IsNullOrEmpty(sessionId))
            {
                await _semanticMemoryService.StoreMessageAsync(sessionId, botResponse, "bot");
            }

            // Step 6: Analyze intent and save history (placeholder)
            var (intent, entities, confidence) = await _intentService.AnalyzeIntentAsync(userMessage, relevantHistoricalMessages);

            // Save chat history
            var chatEntry = new ChatHistory
            {
                SessionId = sessionId ?? Guid.NewGuid().ToString(),
                UserMessage = userMessage,
                BotResponse = botResponse,
                Timestamp = DateTime.UtcNow,
                Intent = intent,
                Entities = entities != null ? string.Join(",", entities) : null,
                Confidence = confidence
            };
            await _chatHistoryRepository.SaveChatHistoryAsync(chatEntry);

            return botResponse;
        }
    }
} 