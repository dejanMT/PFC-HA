using Dejan_Camilleri_SWD63B.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dejan_Camilleri_SWD63B.Controllers
{
    [Authorize(Roles = "Technician")]
    public class TechnicianController : Controller
    {
        private readonly ICacheService _cache;
        public TechnicianController(ICacheService cache) => _cache = cache;

        public async Task<IActionResult> Dashboard()
        {
            var all = await _cache.GetTicketsAsync();
            // keep only open OR less than 7 days old
            var view = all
              .Where(t => !t.ClosedTicket
                       || (DateTimeOffset.UtcNow - t.PostDate) <= TimeSpan.FromDays(7))
              .OrderBy(t => t.PostDate)
              .ToList();

            return View(view);
        }
    }
}
