using Dejan_Camilleri_SWD63B.DataAccess;
using Dejan_Camilleri_SWD63B.Interfaces;
using Dejan_Camilleri_SWD63B.Models;
using Google.Apis.Storage.v1.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using System.Globalization;
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

        /// <summary>
        /// This action is for uploading images to the ticket.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="ticketId"></param>
        /// <returns></returns>
        [HttpPost, Route("Support/UploadImage")]
        public async Task<IActionResult> UploadImage(IFormFile file, string ticketId)
        {
            // put each image under the ticket’s “folder” in the bucket:
            var objectName = $"{ticketId}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var url = await _uploader.UploadFileAsync(file, objectName, User.FindFirst(ClaimTypes.Email)?.Value);
            return Ok(new { imageUrl = url });
        }


        /// <summary>
        /// This action is for opening a new ticket.
        /// </summary>
        /// <param name="ticket"></param>
        /// <returns></returns>
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

        /// <summary>
        /// This action is for the technician to refresh the tickets list.
        /// </summary>
        /// <returns></returns>
        [HttpPost("/cron/refresh-tickets")]
        [AllowAnonymous]  
        public IActionResult RefreshTickets()
        {
            // clear the cached list (so next List() repopulates from Firestore)
            _cache.RemoveTickets();
            return Ok("Tickets cache cleared");
        }

        /// <summary>
        /// This action is for the technician to view the tickets.
        /// </summary>
        /// <param name="priority"></param>
        /// <returns></returns>
        [HttpGet("List")]
        [Authorize(Roles = "Technician")]
        public async Task<IActionResult> List(string priority)
        {
            // still archive old ones on each view
            await _repo.ArchiveOldClosedTicketsAsync();

            // fetch from cache
            var tickets = await _cache.GetTicketsAsync();

            if (tickets == null || !tickets.Any())
            {
                // cache miss: load from Firestore and repopulate cache
                tickets = await _repo.GetTickets();
                await _cache.SetTicketsAsync(tickets);
            }

            // your existing priority‐filtering…
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

        //[HttpGet("List")]
        //[Authorize(Roles = "Technician")]
        //public async Task<IActionResult> List(string priority)
        //{
        //    // archive old 1 week old tickets
        //    await _repo.ArchiveOldClosedTicketsAsync();

        //    //get tickets from cache
        //    var tickets = await _cache.GetTicketsAsync();

        //    //if cache is empty, load from Firestore
        //    if (tickets == null || !tickets.Any())
        //    {
        //        tickets = await _repo.GetTickets();
        //        await _cache.SetTicketsAsync(tickets); //populate the cache
        //    }

        //    //filter tickets by priority
        //    if (!string.IsNullOrEmpty(priority))
        //    {
        //        tickets = tickets
        //            .Where(t => t.Priority.Equals(priority, StringComparison.OrdinalIgnoreCase))
        //            .ToList();
        //    }
        //    else
        //    {
        //        tickets = tickets
        //            .OrderBy(t => t.Priority == "High" ? 0
        //                       : t.Priority == "Medium" ? 1
        //                       : 2)
        //            .ToList();
        //    }

        //    ViewData["SelectedPriority"] = priority;
        //    return View(tickets);
        //}

        /// <summary>
        /// This action is for the technician to take/assign to a ticket.
        /// </summary>
        /// <param name="ticketId"></param>
        /// <returns></returns>
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

        /// <summary>
        /// This action is for the technician to close a complete  a ticket.
        /// </summary>
        /// <param name="ticketId"></param>
        /// <returns></returns>
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
            //check if the ticketId is valid
            var ticket = await _repo.GetTicketByIdAsync(ticketId);
            if (ticket == null) return NotFound();

            // check if the user is allowed to see this ticket
            if (!User.IsInRole("Technician") && ticket.PostAuthorEmail != User.FindFirst(ClaimTypes.Email)?.Value)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            // get the images for this ticket
            foreach (var obj in await _uploader.ListObjectsAsync($"{ticketId}/"))
            {
                var signedUrl = await _uploader.GetSignedUrlAsync(obj.Name, TimeSpan.FromMinutes(15));
                ticket.TicketImageUrls.Add(signedUrl);
            }

            return View(ticket);
        }


        /// <summary>
        /// This action is for the technician to get a screenshot of a ticket.
        /// </summary>
        /// <param name="ticketId"></param>
        /// <param name="objectName"></param>
        /// <returns></returns>
        [Authorize]
        public async Task<IActionResult> GetScreenshot(string ticketId, string objectName)
        {
            var ticket = await _repo.GetTicketByIdAsync(ticketId);
            if (ticket == null)
                return NotFound();

            var email = User.FindFirstValue(ClaimTypes.Email);
            var isOwner = string.Equals(ticket.PostAuthorEmail, email, StringComparison.OrdinalIgnoreCase);
            var isTech = User.IsInRole("Technician");
            if (!isOwner && !isTech)
                return Forbid();

            string signedUrl = await _uploader.GetSignedUrlAsync(objectName, TimeSpan.FromMinutes(15));

            return Redirect(signedUrl);
        }





    }
}