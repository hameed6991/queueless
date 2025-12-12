using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Queueless.Services;

namespace Queueless.Controllers
{
    [ApiController]
    [Route("api/customer/queue")]
    public class CustomerQueueController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly FcmService _fcm;
        private readonly ILogger<CustomerQueueController> _logger;

        public CustomerQueueController(
            IConfiguration config,
            FcmService fcm,
            ILogger<CustomerQueueController> logger)
        {
            _connectionString = config.GetConnectionString("DefaultConnection");
            _fcm = fcm;
            _logger = logger;
        }

        // DTO from Flutter
        public class JoinQueueRequest
        {
            public int BusinessId { get; set; }
            public int CustomerUserId { get; set; }
        }

        // What SP returns
        public class JoinQueueResult
        {
            public int TokenId { get; set; }
            public int BusinessId { get; set; }
            public int CustomerUserId { get; set; }
            public int TokenNumber { get; set; }
            public string Status { get; set; } = "";
            public DateTime CreatedOn { get; set; }
            public int WaitingAhead { get; set; }
            public int? EstimatedWaitMinutes { get; set; }
            public string BusinessName { get; set; } = "";
            public string Category { get; set; } = "";
            public string Emirate { get; set; } = "";
            public string Area { get; set; } = "";
        }

        [HttpPost("join")]
        public async Task<ActionResult<JoinQueueResult>> Join([FromBody] JoinQueueRequest dto)
        {
            if (dto.BusinessId <= 0 || dto.CustomerUserId <= 0)
                return BadRequest("BusinessId and CustomerUserId are required.");

            JoinQueueResult result;

            await using var con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            // ---------------- 1) Run SP and map result ----------------
            await using (var cmd = new SqlCommand("Customer_JoinQueue", con)
            {
                CommandType = CommandType.StoredProcedure
            })
            {
                cmd.Parameters.AddWithValue("@BusinessId", dto.BusinessId);
                cmd.Parameters.AddWithValue("@CustomerUserId", dto.CustomerUserId);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    _logger.LogError("Customer_JoinQueue returned no rows.");
                    return StatusCode(500, "Could not create or fetch token.");
                }

                result = new JoinQueueResult
                {
                    TokenId = reader.GetInt32(reader.GetOrdinal("TokenId")),
                    BusinessId = reader.GetInt32(reader.GetOrdinal("BusinessId")),
                    CustomerUserId = reader.GetInt32(reader.GetOrdinal("CustomerUserId")),
                    TokenNumber = reader.GetInt32(reader.GetOrdinal("TokenNumber")),
                    Status = reader.GetString(reader.GetOrdinal("Status")),
                    CreatedOn = reader.GetDateTime(reader.GetOrdinal("CreatedOn")),
                    WaitingAhead = reader.GetInt32(reader.GetOrdinal("WaitingAhead")),
                    EstimatedWaitMinutes = reader.IsDBNull(reader.GetOrdinal("EstimatedWaitMinutes"))
                        ? (int?)null
                        : reader.GetInt32(reader.GetOrdinal("EstimatedWaitMinutes")),
                    BusinessName = reader.GetString(reader.GetOrdinal("BusinessName")),
                    Category = reader.GetString(reader.GetOrdinal("Category")),
                    Emirate = reader.GetString(reader.GetOrdinal("Emirate")),
                    Area = reader.GetString(reader.GetOrdinal("Area"))
                };
            }

            // ---------------- 2) Notify CUSTOMER (their token created) ----------------
            string? customerFcmToken = null;
            await using (var tokenCmd = new SqlCommand(@"
                SELECT FcmToken
                FROM   AppUser
                WHERE  UserId = @UserId
                  AND  FcmToken IS NOT NULL;", con))
            {
                tokenCmd.Parameters.AddWithValue("@UserId", dto.CustomerUserId);
                var obj = await tokenCmd.ExecuteScalarAsync();
                if (obj != null && obj != DBNull.Value)
                    customerFcmToken = (string)obj;
            }

            if (!string.IsNullOrWhiteSpace(customerFcmToken))
            {
                var data = new Dictionary<string, string>
                {
                    ["type"] = "TOKEN_CREATED",
                    ["tokenId"] = result.TokenId.ToString(),
                    ["tokenNumber"] = result.TokenNumber.ToString(),
                    ["businessId"] = result.BusinessId.ToString(),
                    ["businessName"] = result.BusinessName,
                    ["waitingAhead"] = result.WaitingAhead.ToString(),
                    ["estimatedWaitMinutes"] =
                        result.EstimatedWaitMinutes?.ToString() ?? ""
                };

                await _fcm.SendAsync(
                    customerFcmToken,
                    $"Token created at {result.BusinessName}",
                    $"Your token number is {result.TokenNumber}.",
                    data);
            }

            // ---------------- 3) Notify BUSINESS OWNER ----------------
            int? ownerUserId = null;
            await using (var bizCmd = new SqlCommand(@"
                SELECT TOP 1 OwnerUserId
                FROM BusinessRegistration
                WHERE BusinessId = @BusinessId AND IsActive = 1;", con))
            {
                bizCmd.Parameters.AddWithValue("@BusinessId", dto.BusinessId);
                var obj = await bizCmd.ExecuteScalarAsync();
                if (obj != null && obj != DBNull.Value)
                    ownerUserId = Convert.ToInt32(obj);
            }

            if (ownerUserId.HasValue)
            {
                // Possibly multiple devices for same owner
                var ownerTokens = new List<string>();
                await using (var ownerTokenCmd = new SqlCommand(@"
                    SELECT FcmToken
                    FROM   AppUser
                    WHERE  UserId = @OwnerUserId
                      AND  FcmToken IS NOT NULL;", con))
                {
                    ownerTokenCmd.Parameters.AddWithValue("@OwnerUserId", ownerUserId.Value);

                    await using var r = await ownerTokenCmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                    {
                        var t = r.GetString(0);
                        if (!string.IsNullOrWhiteSpace(t))
                            ownerTokens.Add(t);
                    }
                }
                //comments

                if (ownerTokens.Count == 0)
                {
                    _logger.LogWarning(
                        "No FCM tokens for business owner {OwnerUserId}, skip business push.",
                        ownerUserId.Value);
                }
                else
                {
                    var bizData = new Dictionary<string, string>
                    {
                        ["type"] = "NEW_QUEUE_TOKEN",
                        ["tokenId"] = result.TokenId.ToString(),
                        ["tokenNumber"] = result.TokenNumber.ToString(),
                        ["businessId"] = result.BusinessId.ToString(),
                        ["customerUserId"] = result.CustomerUserId.ToString(),
                        ["createdOn"] = result.CreatedOn.ToString("o")
                    };

                    foreach (var t in ownerTokens)
                    {
                        await _fcm.SendAsync(
                            t,
                            "New customer joined your queue",
                            $"Token C-{result.TokenNumber} joined at {result.BusinessName}.",
                            bizData);
                    }
                }
            }

            // ---------------- 4) Return token info ----------------
            return Ok(result);
        }
    }
}
