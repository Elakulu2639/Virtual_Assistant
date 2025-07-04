using Microsoft.AspNetCore.Mvc;
using ChatBot.Server.Models;
using ChatBot.Server.Services;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ChatBot.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IChatModelService _chatModelService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(IChatModelService chatModelService, ILogger<ChatController> logger)
        {
            _chatModelService = chatModelService;
            _logger = logger;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] ChatInputDto message)
        {
            try
            {
                _logger.LogInformation("Received message: {Message}", message.UserMessage);

                if (string.IsNullOrWhiteSpace(message.UserMessage))
                {
                    return BadRequest(ApiResponse<string>.CreateError("Message cannot be empty", new[] { "Message is required" }));
                }

                // Generate a new session ID if it's the start of a new conversation
                var sessionId = string.IsNullOrEmpty(message.SessionId) ? Guid.NewGuid().ToString() : message.SessionId;

                var response = await _chatModelService.GetChatResponseAsync(message.UserMessage, sessionId);
                
                _logger.LogInformation("Generated response: {Response}", response);

                if (string.IsNullOrWhiteSpace(response))
                {
                    return Ok(ApiResponse<string>.CreateError("No response generated", new[] { "Could not generate a response" }));
                }

                // Include the sessionId in the response
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Message processed successfully",
                    Data = response,
                    SessionId = sessionId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message: {Message}", message.UserMessage);
                return StatusCode(500, ApiResponse<string>.CreateError(
                    "I apologize, but I encountered an error while processing your message. Please try again in a moment.",
                    new[] { ex.Message }
                ));
            }
        }
    }
}
