using Microsoft.AspNetCore.Mvc;

namespace SmallShopBigAmbitions.Business.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WebHookController : ControllerBase
    {
        // Example webhook that is idempotent by event id
        [HttpPost("provider/{provider}/event")]
        public IActionResult ReceiveEvent(string provider, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromBody] object payload)
        {
            // In a real handler, map to a command implementing IIdempotentRequest with scope $"webhook:{provider}" and forward to dispatcher
            return Ok(new { received = true, provider, idempotencyKey = idempotencyKey ?? string.Empty });
        }
    }
}
