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

        public NotificationController(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection");
        }

        [HttpPost("register-token")]
        public async Task<IActionResult> RegisterToken([FromBody] RegisterTokenDto dto)
        {
            if (dto.UserId <= 0 || string.IsNullOrWhiteSpace(dto.FcmToken))
                return BadRequest("UserId and FcmToken are required.");

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
          UPDATE appuser
             SET FcmToken = @FcmToken
           WHERE userid = @UserId;", con);

            cmd.Parameters.AddWithValue("@UserId", dto.UserId);
            cmd.Parameters.AddWithValue("@FcmToken", dto.FcmToken);

            await con.OpenAsync();
            var rows = await cmd.ExecuteNonQueryAsync();

            if (rows == 0) return NotFound("User not found.");
            return Ok(new { success = true });
        }
    }
}
