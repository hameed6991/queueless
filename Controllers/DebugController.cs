using Microsoft.AspNetCore.Mvc;
using Queueless.Services;

namespace Queueless.Controllers
{
    public class TestFcmRequest
    {
        public string FcmToken { get; set; } = string.Empty;
    }

    [ApiController]
    [Route("api/debug")]
    public class DebugController : ControllerBase
    {
        private readonly FcmService _fcm;

        public DebugController(FcmService fcm)
        {
            _fcm = fcm;
        }

        [HttpPost("test-fcm")]
        public async Task<IActionResult> TestFcm([FromBody] TestFcmRequest dto)
        {
            var data = new Dictionary<string, string>
            {
                ["type"] = "TEST"
            };

            await _fcm.SendAsync(
                dto.FcmToken,
                "Test from Backend",
                "If you see this, FCM backend is working",
                data   // ✅ now matches IReadOnlyDictionary<string,string>
            );

            return Ok(new { status = "sent" });
        }

    }
}
