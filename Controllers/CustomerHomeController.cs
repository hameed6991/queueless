using Microsoft.AspNetCore.Mvc;

using QueueLess.Api.Models;
using Queueless.Services;

namespace QueueLess.Api.Controllers
{
    [ApiController]
    [Route("api/customer/home")]
    public class CustomerHomeController : ControllerBase
    {
        private readonly ICustomerHomeService _service;

        public CustomerHomeController(ICustomerHomeService service)
        {
            _service = service;
        }

        // GET: api/customer/home/nearby?lat=25.27&lon=55.30&radiusKm=5&category=Typing%20Centre
        [HttpGet("nearby")]
        public async Task<ActionResult<List<NearbyBusinessDto>>> GetNearby(
            [FromQuery] decimal lat,
            [FromQuery] decimal lon,
            [FromQuery] double radiusKm = 5,
            [FromQuery] string? category = null)
        {
            if (lat == 0 || lon == 0)
                return BadRequest("lat and lon are required.");

            var result = await _service.GetNearbyBusinessesAsync(lat, lon, radiusKm, category);
            return Ok(result);
        }
    }
}
