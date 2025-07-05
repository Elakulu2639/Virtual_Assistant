using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace ChatBot.Server.Services
{
    public class LLMService : ILLMService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<LLMService> _logger;

        public LLMService(HttpClient httpClient, ILogger<LLMService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            //_httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
        }

        public async Task<string> GetLLMResponseAsync(List<object> messages, string model, double temperature, int maxTokens, double topP, double presencePenalty, double frequencyPenalty)
        {
            var jsonPayload = JsonSerializer.Serialize(new
            {
                model = model,
                messages = messages,
                temperature = temperature,
                max_tokens = maxTokens,
                top_p = topP,
                presence_penalty = presencePenalty,
                frequency_penalty = frequencyPenalty
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
            return botResponse;
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
            return null;
        }
    }
} 