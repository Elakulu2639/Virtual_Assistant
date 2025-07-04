using Microsoft.AspNetCore.Mvc;
using ChatBot.Server.Services;
using System.Threading.Tasks;

namespace ChatBot.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DomainController : ControllerBase
    {
        // Remove NlpService dependency and related code. Use modular services if needed.
    }

    public class ChangeDomainRequest
    {
        public string Domain { get; set; }
    }
} 