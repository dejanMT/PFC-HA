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
        //public async Task<IActionResult> UploadImage(IFormFile file)
        //{
        //    var url = await _uploader.UploadFileAsync(file, null);

        //    return Ok(new { imageUrl = url });
        //}

        public async Task<IActionResult> UploadImage(IFormFile file, string ticketId)
        {
            // put each image under the ticket’s “folder” in the bucket:
            var objectName = $"{ticketId}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var url = await _uploader.UploadFileAsync(file, objectName);
            return Ok(new { imageUrl = url });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OpenTicket(TicketPost ticket)
        {
            ticket.TicketId = Guid.NewGuid().ToString();
            ticket.OpenDate = DateTimeOffset.UtcNow;
            ticket.PostAuthor = User.Identity.Name;
            ticket.PostAuthorEmail = User.FindFirst(ClaimTypes.Email)?.Value;


            var payload = new
            {
                ticket.TicketId,
                ticket.PostTitle,
                ticket.PostDescription,
                ticket.PostAuthorEmail,
                ticket.Priority,
                ticket.OpenDate,
                ticket.TicketImageUrls,
                Status = "Queued"
            };

            await _pubsub.PublishTicketAsync(payload, ticket.Priority);

            await _repo.AddTicket(ticket);

            return RedirectToAction("Index", "Home");
        }


        [HttpGet("List")]
        [Authorize(Roles = "Technician")]
        public async Task<IActionResult> List(string priority = "")
        {
            // archive old 1 week old tickets
            await _repo.ArchiveOldClosedTicketsAsync();

            //get tickets from cache
            var tickets = await _cache.GetTicketsAsync();

            //if cache is empty, load from Firestore
            if (tickets == null || !tickets.Any())
            { 
                tickets = await _repo.GetTickets();
                await _cache.SetTicketsAsync(tickets); //populate the cache
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

        [HttpPost("TakeTicket/{ticketId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TakeTicket(string ticketId)
        {
            var tech = User.FindFirst(ClaimTypes.Email)?.Value;
            await _repo.UpdateTicketAsync(ticketId, supportAgent: tech);

            // update cache so UI shows the change immediately
            var tickets = await _cache.GetTicketsAsync();
            var t = tickets.FirstOrDefault(x => x.TicketId == ticketId);
            if (t != null)
            {
                t.SupportAgent = tech;
                t.CloseDate = DateTimeOffset.UtcNow;
                await _cache.SetTicketsAsync(tickets);
            }

            return RedirectToAction("List");
        }

        [HttpPost("MyTickets/{ticketId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CloseTicket(string ticketId)
        {
            await _repo.UpdateTicketAsync(ticketId, closed: true, closeDate: DateTimeOffset.UtcNow);

            // update cache
            var tickets = await _cache.GetTicketsAsync();
            var t = tickets.FirstOrDefault(x => x.TicketId == ticketId);
            if (t != null)
            {
                t.ClosedTicket = true;
                t.CloseDate = DateTimeOffset.UtcNow;
                await _cache.SetTicketsAsync(tickets);
            }

            return RedirectToAction("List");
        }


        /// <summary>
        /// This action is for the support agent and user to view their own tickets.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> MyTickets()
        {
            var currentEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            var all = await _repo.GetTickets();

            // include *both* open and closed for me
            var mine = all
                .Where(t => t.PostAuthorEmail == currentEmail
                         || t.SupportAgent == currentEmail)
                .ToList();

            return View(mine);
        }


        /// <summary>
        /// This will show the details of a ticket.
        /// </summary>
        /// <param name="ticketId"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Details(string ticketId)
        {
            if (string.IsNullOrEmpty(ticketId)) return BadRequest();

            var ticket = await _repo.GetTicketByIdAsync(ticketId);
            if (ticket == null) return NotFound();

            //ticket.TicketImageUrls = new List<string>();
            foreach (var obj in await _uploader.ListObjectsAsync($"{ticketId}/"))
            {
                var signedUrl = await _uploader.GetSignedUrlAsync(obj.Name, TimeSpan.FromMinutes(15));
                ticket.TicketImageUrls.Add(signedUrl);
            }

            return View(ticket);
        }





    }
}