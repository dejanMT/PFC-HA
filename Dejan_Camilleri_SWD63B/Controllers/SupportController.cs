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

        public SupportController(FirestoreRepository repo, IFileUploadService uploader, IPubSubService pubsub)
        {
            _repo = repo;
            _uploader = uploader;
            _pubsub = pubsub;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OpenTicket(TicketPost ticket)
        {

            ticket.TicketId = Guid.NewGuid().ToString();
            ticket.PostDate = DateTimeOffset.UtcNow;
            ticket.PostAuthor = User.Identity.Name;
            ticket.Priority = ticket.Priority;
            ticket.PostAuthorEmail = User.FindFirst(ClaimTypes.Email)?.Value;

            if (ticket. TicketImage != null && ticket.TicketImage.Length > 0)
            {
                ticket.TicketImageUrl = await _uploader.UploadFileAsync(ticket.TicketImage, null);
            }

            //PUB/SUB topic 
            var payload = new
            {
                ticket.TicketId,
                ticket.PostTitle,
                ticket.PostDescription,
                ticket.PostAuthorEmail,
                ticket.Priority,
                ticket.PostDate,
                ticket.TicketImageUrl,
                Status = "Queued"
            };

            // publish to Pub/Sub
            await _pubsub.PublishTicketAsync(payload, ticket.Priority);

            // save to Firestore
            await _repo.AddTicket(ticket);

            return Ok(new
            {
                redirectUrl = Url.Action("List", "Support")
            });
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var tickets = await _repo.GetTickets();
            return View(tickets);
        }


    }
}