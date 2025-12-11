using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Queueless.Models.Auth;
using Queueless.Models.Dtos;
using System.Data;

namespace Queueless.Controllers
{

    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly string _connectionString;

        public AuthController(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection");
        }
        [HttpPost("complete-profile")]
        public IActionResult CompleteProfile([FromBody] CompleteProfileDto dto)
        {
            if (dto == null || dto.UserId <= 0 || string.IsNullOrWhiteSpace(dto.FullName))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid profile data."
                });
            }

            // TODO: hook this into your real DB tables.
            // For now we just pretend it is saved successfully.

            // Example of where you'd normally update DB:
            // var user = await _db.AppUsers.FindAsync(dto.UserId);
            // if (user == null) { return NotFound(...); }
            // user.FullName = dto.FullName;
            // user.Email = dto.Email;
            // user.IsBusinessOwner = dto.IsBusinessOwner;
            // await _db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Profile saved successfully.",
                userId = dto.UserId,
                isBusinessOwner = dto.IsBusinessOwner
            });
        }
        // ============================================================
        //  POST /api/auth/request-otp
        //  Inserts OTP row into UserOtp for reference (optional)
        // ============================================================
        [HttpPost("verify-otp")]
        public async Task<ActionResult<VerifyOtpResponseDto>> VerifyOtp([FromBody] VerifyOtpRequestDto dto)
        {
            var response = new VerifyOtpResponseDto();

            if (dto == null ||
                string.IsNullOrWhiteSpace(dto.MobileNumber) ||
                string.IsNullOrWhiteSpace(dto.Otp))
            {
                response.Success = false;
                response.Message = "MobileNumber and Otp are required.";
                return BadRequest(response);
            }

            try
            {
                // 1) OTP check (for now: fixed "1234")
                if (dto.Otp != "1234")
                {
                    response.Success = false;
                    response.Message = "Invalid OTP.";
                    return Ok(response);
                }

                // 2) Normalise login type ("Customer" / "Business")
                var cleanLoginType = string.IsNullOrWhiteSpace(dto.LoginType)
                    ? "Customer"
                    : dto.LoginType!.Trim();

                var roleForNewUser = cleanLoginType.Equals("Business", StringComparison.OrdinalIgnoreCase)
                    ? "Business"
                    : "Customer";

                using var con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                long userId;
                bool isNewUser;
                string currentRole = "Customer";

                // 3) Try to find existing user by mobile
                using (var cmdFind = new SqlCommand(
                           "SELECT TOP 1 UserId, Role FROM AppUser WHERE MobileNumber = @MobileNumber",
                           con))
                {
                    cmdFind.Parameters.AddWithValue("@MobileNumber", dto.MobileNumber);

                    using var reader = await cmdFind.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        userId = reader.GetInt64(0);
                        currentRole = reader.IsDBNull(1) ? "Customer" : reader.GetString(1);
                        isNewUser = false;
                    }
                    else
                    {
                        // close reader before new command
                        reader.Close();

                        // 4) Not found -> create new user row with role based on login type
                        using var cmdInsert = new SqlCommand(@"
                    INSERT INTO AppUser (MobileNumber, Role, IsActive, CreatedAtUtc)
                    VALUES (@MobileNumber, @Role, 1, SYSUTCDATETIME());
                    SELECT CAST(SCOPE_IDENTITY() AS BIGINT);", con);

                        cmdInsert.Parameters.AddWithValue("@MobileNumber", dto.MobileNumber);
                        cmdInsert.Parameters.AddWithValue("@Role", roleForNewUser);

                        var newIdObj = await cmdInsert.ExecuteScalarAsync();
                        if (newIdObj == null || newIdObj == DBNull.Value)
                        {
                            response.Success = false;
                            response.Message = "Failed to create user.";
                            return StatusCode(500, response);
                        }

                        userId = Convert.ToInt64(newIdObj);
                        isNewUser = true;
                        currentRole = roleForNewUser;
                    }
                }

                // 5) Update last login
                using (var cmdUpdate = new SqlCommand(
                           "UPDATE AppUser SET LastLoginUtc = SYSUTCDATETIME() WHERE UserId = @UserId",
                           con))
                {
                    cmdUpdate.Parameters.AddWithValue("@UserId", userId);
                    await cmdUpdate.ExecuteNonQueryAsync();
                }

                // 6) Check if user has any active business
                bool hasBusiness;
                using (var cmdBiz = new SqlCommand(@"
            IF EXISTS (SELECT 1 FROM Business WHERE OwnerUserId = @UserId AND IsActive = 1)
                SELECT 1
            ELSE
                SELECT 0;", con))
                {
                    cmdBiz.Parameters.AddWithValue("@UserId", userId);
                    var bizResult = await cmdBiz.ExecuteScalarAsync();
                    hasBusiness = (bizResult != null && bizResult != DBNull.Value &&
                                   Convert.ToInt32(bizResult) == 1);
                }

                // 7) Build response
                response.Success = true;
                response.Message = "OTP verified.";
                response.UserId = userId;
                response.IsNewUser = isNewUser;
                response.HasBusiness = hasBusiness;
                response.Role = currentRole ?? "Customer";

                return Ok(response);
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = "Server error while verifying OTP: " + ex.Message;
                return StatusCode(500, response);
            }
        }


        // ============================================================
        //  POST /api/auth/verify-otp
        //  Simple: check OTP == "1234", then create/find AppUser
        // ============================================================


    }
}
