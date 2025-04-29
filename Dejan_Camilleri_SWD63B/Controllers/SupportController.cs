using Dejan_Camilleri_SWD63B.DataAccess;
using Dejan_Camilleri_SWD63B.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Dejan_Camilleri_SWD63B.Controllers
{
    [Authorize]
    public class SupportController : Controller
    {
        private FirestoreRepository _repo;

        public SupportController(FirestoreRepository repo)
        {
            _repo = repo;
        }

        public IActionResult Index()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OpenTicket(TicketPost ticket)
        {
            if (!ModelState.IsValid)
                return View("Index", ticket);

            ticket.TicketId = Guid.NewGuid().ToString();
            ticket.PostDate = DateTimeOffset.UtcNow;
            ticket.PostAuthor = User.Identity.Name;
            ticket.PostAuthorEmail = User.FindFirst(ClaimTypes.Email)?.Value;

            await _repo.AddTicket(ticket);  
            return RedirectToAction("Index"); 
        }


    }
}