using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;      // 👈 add this
using Queueless.Data;
using Queueless.Models.Business;
using Queueless.Models.Business.Dtos;

namespace Queueless.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BusinessController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly string _connectionString;   // 👈 new field

        public BusinessController(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _connectionString = config.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException(
                                    "Connection string 'DefaultConnection' not found.");
        }

        // POST: api/Business/register
        [HttpPost("register")]
        public async Task<IActionResult> RegisterBusiness(
            [FromBody] BusinessRegistrationRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // OPTIONAL: check if this user already has a business
            var existing = await _db.BusinessRegistrations
                .FirstOrDefaultAsync(b => b.OwnerUserId == req.UserId && b.IsActive);

            if (existing != null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "You already have a registered business."
                });
            }

            var entity = new BusinessRegistration
            {
                OwnerUserId = req.UserId,
                BusinessName = req.BusinessName.Trim(),
                Category = req.Category.Trim(),

                Emirate = req.Emirate.Trim(),
                Area = req.Area.Trim(),
                BuildingName = req.Building.Trim(),
                Landmark = string.IsNullOrWhiteSpace(req.Landmark)
                                ? null
                                : req.Landmark.Trim(),

                Latitude = (decimal)req.Latitude,
                Longitude = (decimal)req.Longitude,

                ContactPersonName = req.ContactName.Trim(),
                ContactMobile = req.ContactPhone.Trim(),
                ContactWhatsapp = string.IsNullOrWhiteSpace(req.ContactWhatsapp)
                                ? null
                                : req.ContactWhatsapp.Trim(),

                AvgTimeMinutes = req.AvgTimeMinutes,
                TradeLicenseImagePath = string.IsNullOrWhiteSpace(req.TradeLicenseImagePath)
                                ? null
                                : req.TradeLicenseImagePath.Trim(),

                CreatedOn = DateTime.Now,
                CreatedBy = "mobile-app",
                IsActive = true
            };

            _db.BusinessRegistrations.Add(entity);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Business registered successfully",
                businessId = entity.BusinessId
            });
        }

        // GET: api/Business/dashboard?ownerUserId=90
        [HttpGet("dashboard")]
        public async Task<ActionResult<BusinessDashboardDto>> GetDashboard(
            [FromQuery] int ownerUserId)
        {
            if (ownerUserId <= 0)
            {
                return BadRequest("ownerUserId is required.");
            }

            using var con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            // 1) Find active business for this owner
            int businessId;
            string businessName;
            string category;
            string area;
            int? avgServiceTime = null;

            using (var cmdBiz = new SqlCommand(@"
                SELECT TOP 1 
                    BusinessId, 
                    BusinessName, 
                    Category, 
                    Area, 
                    AvgTimeMinutes
                FROM BusinessRegistration
                WHERE OwnerUserId = @OwnerUserId AND IsActive = 1
                ORDER BY CreatedOn DESC;", con))
            {
                cmdBiz.Parameters.AddWithValue("@OwnerUserId", ownerUserId);

                using var r = await cmdBiz.ExecuteReaderAsync();
                if (!await r.ReadAsync())
                {
                    return NotFound("No active business for this owner.");
                }

                businessId = r.GetInt32(0);
                businessName = r.IsDBNull(1) ? "My Business" : r.GetString(1);
                category = r.IsDBNull(2) ? "" : r.GetString(2);
                area = r.IsDBNull(3) ? "" : r.GetString(3);
                if (!r.IsDBNull(4)) avgServiceTime = r.GetInt32(4);
            }

            // 2) Current token (today)
            string? currentTokenNumber = null;

            using (var cmdCurrent = new SqlCommand(@"
                SELECT TOP 1 TokenNumber
                FROM ServiceQueueToken
                WHERE BusinessId = @BusinessId
                  AND CAST(CreatedOn AS DATE) = CAST(GETDATE() AS DATE)
                  AND Status IN (N'Serving', N'At counter')
                ORDER BY CreatedOn DESC;", con))
            {
                cmdCurrent.Parameters.AddWithValue("@BusinessId", businessId);
                var result = await cmdCurrent.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    currentTokenNumber = Convert.ToString(result);
                }
            }

            // 3) Today's waiting queue – NO Service join, ServiceName = ''
            var queueItems = new List<BusinessQueueItemDto>();

            using (var cmdQueue = new SqlCommand(@"
                SELECT 
                    t.TokenId,
                    t.TokenNumber,
                    ISNULL(u.FullName, '') AS CustomerName
                FROM ServiceQueueToken t
                LEFT JOIN AppUser u ON u.UserId = t.CustomerUserId
                WHERE t.BusinessId = @BusinessId
                  AND CAST(t.CreatedOn AS DATE) = CAST(GETDATE() AS DATE)
                  AND t.Status = N'Waiting'
                ORDER BY t.CreatedOn ASC;", con))
            {
                cmdQueue.Parameters.AddWithValue("@BusinessId", businessId);

                using var r = await cmdQueue.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var item = new BusinessQueueItemDto
                    {
                        TokenId = r.GetInt32(0),
                        TokenNumber = r.GetInt32(1).ToString(),   // if TokenNumber is INT
                        CustomerName = r.GetString(2),
                        ServiceName = ""                          // empty for now
                    };
                    queueItems.Add(item);
                }
            }

            var dto = new BusinessDashboardDto
            {
                BusinessId = businessId,
                BusinessName = businessName,
                Category = category,
                Area = area,
                AvgWaitMinutes = avgServiceTime,
                CurrentTokenNumber = currentTokenNumber,
                WaitingCount = queueItems.Count,
                Queue = queueItems
            };

            return Ok(dto);
        }
    }
}
