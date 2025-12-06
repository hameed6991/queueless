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
        [HttpPost("request-otp")]
        public async Task<ActionResult<RequestOtpResponse>> RequestOtp([FromBody] RequestOtpDto dto)
        {
            var response = new RequestOtpResponse();

            if (dto == null || string.IsNullOrWhiteSpace(dto.MobileNumber))
            {
                response.Success = false;
                response.Message = "MobileNumber is required.";
                return BadRequest(response);
            }

            try
            {
                // Fixed OTP for now – same as what you type in app
                var otp = "1234";
                var nowUtc = DateTime.UtcNow;
                var expiresAtUtc = nowUtc.AddMinutes(5);

                using var con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                // Optional: store OTP in UserOtp table if you want
                using (var cmd = new SqlCommand(@"
                    INSERT INTO UserOtp (MobileNumber, OtpCode, ExpiresAtUtc, IsUsed)
                    VALUES (@MobileNumber, @OtpCode, @ExpiresAtUtc, 0);", con))
                {
                    cmd.Parameters.AddWithValue("@MobileNumber", dto.MobileNumber);
                    cmd.Parameters.AddWithValue("@OtpCode", otp);
                    cmd.Parameters.AddWithValue("@ExpiresAtUtc", expiresAtUtc);
                    await cmd.ExecuteNonQueryAsync();
                }

                response.Success = true;
                response.Message = "OTP sent.";
                response.IsNewUser = false; // we will decide in verify step
                response.DebugOtp = otp;     // only for testing

                return Ok(response);
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = "Error while sending OTP.";
                response.DebugOtp = ex.Message; // so you can see DB error in Postman if needed
                return StatusCode(500, response);
            }
        }

        // ============================================================
        //  POST /api/auth/verify-otp
        //  Simple: check OTP == "1234", then create/find AppUser
        // ============================================================
        [HttpPost("verify-otp")]
        public async Task<ActionResult<VerifyOtpResponse>> VerifyOtp([FromBody] VerifyOtpDto dto)
        {
            var response = new VerifyOtpResponse();

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

                using var con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                long userId;
                bool isNewUser;

                // 2) Try to find existing user by mobile
                using (var cmdFind = new SqlCommand(
                    "SELECT TOP 1 UserId FROM AppUser WHERE MobileNumber = @MobileNumber",
                    con))
                {
                    cmdFind.Parameters.AddWithValue("@MobileNumber", dto.MobileNumber);

                    var result = await cmdFind.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        userId = Convert.ToInt64(result);
                        isNewUser = false;
                    }
                    else
                    {
                        // 3) Not found -> create new user row
                        using var cmdInsert = new SqlCommand(@"
                            INSERT INTO AppUser (MobileNumber, Role, IsActive, CreatedAtUtc)
                            VALUES (@MobileNumber, 'Customer', 1, SYSUTCDATETIME());
                            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);", con);

                        cmdInsert.Parameters.AddWithValue("@MobileNumber", dto.MobileNumber);
                        var newIdObj = await cmdInsert.ExecuteScalarAsync();

                        if (newIdObj == null || newIdObj == DBNull.Value)
                        {
                            response.Success = false;
                            response.Message = "Failed to create user.";
                            return StatusCode(500, response);
                        }

                        userId = Convert.ToInt64(newIdObj);
                        isNewUser = true;
                    }
                }

                // 4) Update last login
                using (var cmdUpdate = new SqlCommand(
                    "UPDATE AppUser SET LastLoginUtc = SYSUTCDATETIME() WHERE UserId = @UserId",
                    con))
                {
                    cmdUpdate.Parameters.AddWithValue("@UserId", userId);
                    await cmdUpdate.ExecuteNonQueryAsync();
                }

                // 5) Check if user has any active business
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

                response.Success = true;
                response.Message = "OTP verified.";
                response.UserId = userId;
                response.IsNewUser = isNewUser;
                response.HasBusiness = hasBusiness;

                return Ok(response);
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = "Server error while verifying OTP: " + ex.Message;
                return StatusCode(500, response);
            }
        }
    }
}
