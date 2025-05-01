using Dejan_Camilleri_SWD63B.DataAccess;
using Dejan_Camilleri_SWD63B.Interfaces;
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
        private readonly IFileUploadService _uploader;
        private readonly IPubSubService _pubsub;
        private readonly ICacheService _cache;

        public SupportController(FirestoreRepository repo, IFileUploadService uploader, IPubSubService pubsub, ICacheService cache)
        {
            _repo = repo;
            _uploader = uploader;
            _pubsub = pubsub;
            _cache = cache;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost, Route("Support/UploadImage")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            var url = await _uploader.UploadFileAsync(file, null);

            return Ok(new { imageUrl = url });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OpenTicket(TicketPost ticket)
        {
            ticket.TicketId = Guid.NewGuid().ToString();
            ticket.PostDate = DateTimeOffset.UtcNow;
            ticket.PostAuthor = User.Identity.Name;
            ticket.PostAuthorEmail = User.FindFirst(ClaimTypes.Email)?.Value;

            var payload = new
            {
                ticket.TicketId,
                ticket.PostTitle,
                ticket.PostDescription,
                ticket.PostAuthorEmail,
                ticket.Priority,
                ticket.PostDate,
                ticket.TicketImageUrls,
                Status = "Queued"
            };

            await _pubsub.PublishTicketAsync(payload, ticket.Priority);

            await _repo.AddTicket(ticket);

            return RedirectToAction("List", "Support");
        }


        [HttpGet("List")]
        public async Task<IActionResult> List(string priority = "")
        {

            //get tickets from cache
            var tickets = await _cache.GetTicketsAsync();

            //if cache is empty, load from Firestore
            if (tickets == null || !tickets.Any())
            {
                tickets = await _repo.GetTickets();
                await _cache.SetTicketsAsync(tickets); //populate cache
            }

            //filter tickets by priority
            if (!string.IsNullOrEmpty(priority))
            {
                tickets = tickets
                    .Where(t => t.Priority.Equals(priority, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            else
            {
                tickets = tickets
                    .OrderBy(t => t.Priority == "High" ? 0
                               : t.Priority == "Medium" ? 1
                               : 2)
                    .ToList();
            }

            ViewData["SelectedPriority"] = priority;
            return View(tickets);
        }




    }
}