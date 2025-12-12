using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Queueless.Services; // FcmService

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

        // POST: /api/customer/queue/join
        [HttpPost("join")]
        public async Task<ActionResult<JoinQueueResult>> Join([FromBody] JoinQueueRequest dto)
        {
            if (dto.BusinessId <= 0 || dto.CustomerUserId <= 0)
                return BadRequest("BusinessId and CustomerUserId are required.");

            JoinQueueResult result;

            await using var con = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand("Customer_JoinQueue", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@BusinessId", dto.BusinessId);
            cmd.Parameters.AddWithValue("@CustomerUserId", dto.CustomerUserId);

            await con.OpenAsync();

            // 1️⃣ Run SP and map result
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
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

            // 2️⃣ Get FCM token for THIS customer (from AppUser)
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
                {
                    customerFcmToken = (string)obj;
                }
            }

            // 3️⃣ Send "TOKEN_CREATED" notification to customer (if token exists)
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
                    ["estimatedWaitMinutes"] = result.EstimatedWaitMinutes?.ToString() ?? ""
                };

                await _fcm.SendAsync(
                    customerFcmToken,
                    $"Token created at {result.BusinessName}",
                    $"Your token number is {result.TokenNumber}.",
                    data
                );

                _logger.LogInformation(
                    "Sent TOKEN_CREATED notification for TokenId {TokenId} to user {UserId}",
                    result.TokenId, dto.CustomerUserId);
            }
            else
            {
                _logger.LogWarning(
                    "No FcmToken found for CustomerUserId {UserId}, skipping customer push notification.",
                    dto.CustomerUserId);
            }

            // 4️⃣ Notify BUSINESS owner about new / existing token in queue
            try
            {
                // 4a) Find owner of this business
                int? ownerUserId = null;
                await using (var ownerCmd = new SqlCommand(@"
                    SELECT TOP 1 OwnerUserId
                    FROM   BusinessRegistration
                    WHERE  BusinessId = @BusinessId
                      AND  IsActive = 1;", con))
                {
                    ownerCmd.Parameters.AddWithValue("@BusinessId", dto.BusinessId);
                    var ownerObj = await ownerCmd.ExecuteScalarAsync();
                    if (ownerObj != null && ownerObj != DBNull.Value)
                    {
                        ownerUserId = Convert.ToInt32(ownerObj);
                    }
                }

                if (ownerUserId.HasValue)
                {
                    // 4b) Get all FCM tokens for that owner (in case multiple devices later)
                    var businessTokens = new List<string>();

                    await using (var bizTokenCmd = new SqlCommand(@"
                        SELECT FcmToken
                        FROM   AppUser
                        WHERE  UserId = @OwnerUserId
                          AND  FcmToken IS NOT NULL;", con))
                    {
                        bizTokenCmd.Parameters.AddWithValue("@OwnerUserId", ownerUserId.Value);

                        await using var r = await bizTokenCmd.ExecuteReaderAsync();
                        while (await r.ReadAsync())
                        {
                            if (!r.IsDBNull(0))
                            {
                                var token = r.GetString(0);
                                if (!string.IsNullOrWhiteSpace(token))
                                    businessTokens.Add(token);
                            }
                        }
                    }

                    // 4c) Get customer name (for nicer message)
                    string? customerName = null;
                    await using (var custNameCmd = new SqlCommand(@"
                        SELECT FullName
                        FROM   AppUser
                        WHERE  UserId = @UserId;", con))
                    {
                        custNameCmd.Parameters.AddWithValue("@UserId", dto.CustomerUserId);
                        var nameObj = await custNameCmd.ExecuteScalarAsync();
                        if (nameObj != null && nameObj != DBNull.Value)
                        {
                            customerName = (string)nameObj;
                        }
                    }

                    if (businessTokens.Count > 0)
                    {
                        await _fcm.SendQueueUpdateToBusinessAsync(
                            result.BusinessId,
                            result.TokenNumber.ToString(),
                            customerName,
                            businessTokens);

                        _logger.LogInformation(
                            "Sent queue_update notification for BusinessId {BusinessId}, Token {TokenNumber}",
                            result.BusinessId, result.TokenNumber);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "No FCM tokens found for business owner {OwnerUserId} (BusinessId {BusinessId}).",
                            ownerUserId.Value, result.BusinessId);
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "No active BusinessRegistration found for BusinessId {BusinessId} when sending queue_update.",
                        result.BusinessId);
                }
            }
            catch (Exception ex)
            {
                // Don’t fail the API if push fails – just log
                _logger.LogError(ex,
                    "Error while sending queue_update notification for BusinessId {BusinessId}, TokenId {TokenId}",
                    result.BusinessId, result.TokenId);
            }

            // 5️⃣ Return token info back to Flutter (for UI)
            return Ok(result);
        }
    }
}
