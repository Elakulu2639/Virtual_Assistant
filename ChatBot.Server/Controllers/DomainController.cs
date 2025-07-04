using Microsoft.AspNetCore.Mvc;
using ChatBot.Server.Services;
using System.Threading.Tasks;

namespace ChatBot.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DomainController : ControllerBase
    {
        private readonly NlpService _nlpService;

        public DomainController(NlpService nlpService)
        {
            _nlpService = nlpService;
        }

        [HttpGet("available")]
        public async Task<IActionResult> GetAvailableDomains()
        {
            try
            {
                var domains = await _nlpService.GetAvailableDomainsAsync();
                return Ok(domains);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("change")]
        public async Task<IActionResult> ChangeDomain([FromBody] ChangeDomainRequest request)
        {
            try
            {
                var success = await _nlpService.ChangeDomainAsync(request.Domain);
                if (success)
                {
                    return Ok(new { status = "success", domain = request.Domain });
                }
                else
                {
                    return BadRequest(new { error = "Failed to change domain" });
                }
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    public class ChangeDomainRequest
    {
        public string Domain { get; set; }
    }
} 