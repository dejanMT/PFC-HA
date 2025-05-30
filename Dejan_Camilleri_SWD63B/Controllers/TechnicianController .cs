﻿using Dejan_Camilleri_SWD63B.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dejan_Camilleri_SWD63B.Controllers
{

    /// <summary>
    /// TechnicianController is responsible for handling requests related to the technician dashboard.
    /// </summary>
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
                       || (DateTimeOffset.UtcNow - t.OpenDate) <= TimeSpan.FromDays(7))
              .OrderBy(t => t.OpenDate)
              .ToList();

            return View(view);
        }
    }
}
