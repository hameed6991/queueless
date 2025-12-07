using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

using Queueless.Models;

namespace Queueless.Controllers
{
    [ApiController]
    [Route("api/notification")]
    public class NotificationController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly ILogger<NotificationController> _logger;

        public NotificationController(IConfiguration config,
                                      ILogger<NotificationController> logger)
        {
            _connectionString = config.GetConnectionString("DefaultConnection");
            _logger = logger;
        }

        [HttpPost("register-token")]
        public async Task<IActionResult> RegisterToken([FromBody] RegisterTokenDto dto)
        {
            if (dto == null || dto.UserId <= 0 || string.IsNullOrWhiteSpace(dto.FcmToken))
            {
                _logger.LogWarning("RegisterToken: invalid dto {@dto}", dto);
                return BadRequest("UserId and FcmToken are required.");
            }

            _logger.LogInformation("RegisterToken: user {UserId}, token {Token}",
                dto.UserId, dto.FcmToken);

            using var con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            // 1) Update main AppUser table
            using (var cmd = new SqlCommand(@"
UPDATE dbo.AppUser
   SET FcmToken = @FcmToken
 WHERE UserId = @UserId;", con))
            {
                cmd.Parameters.AddWithValue("@UserId", dto.UserId);
                cmd.Parameters.AddWithValue("@FcmToken", dto.FcmToken);

                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0)
                {
                    _logger.LogWarning("RegisterToken: user {UserId} not found", dto.UserId);
                    return NotFound("User not found.");
                }
            }

            // 2) Optional: keep history of multiple devices
            using (var cmd2 = new SqlCommand(@"
IF NOT EXISTS (
    SELECT 1 FROM dbo.FcmDeviceTokens
    WHERE UserId = @UserId AND DeviceToken = @Token AND IsActive = 1
)
BEGIN
    INSERT INTO dbo.FcmDeviceTokens (UserId, DeviceToken, Platform, IsActive, CreatedAtUtc)
    VALUES (@UserId, @Token, @Platform, 1, SYSUTCDATETIME());
END
", con))
            {
                cmd2.Parameters.AddWithValue("@UserId", dto.UserId);
                cmd2.Parameters.AddWithValue("@Token", dto.FcmToken);
                cmd2.Parameters.AddWithValue("@Platform", (object?)dto.Platform ?? DBNull.Value);
                await cmd2.ExecuteNonQueryAsync();
            }

            return Ok(new { success = true });
        }
    }
}
