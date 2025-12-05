using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Queueless.Data;
using Queueless.Models.Business;

namespace Queueless.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BusinessController : ControllerBase
    {
        private readonly AppDbContext _db;

        public BusinessController(AppDbContext db)
        {
            _db = db;
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
                CreatedBy = "mobile-app",   // later: set from user mobile/username
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
    }
}
