using Dejan_Camilleri_SWD63B.Models;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace Dejan_Camilleri_SWD63B.DataAccess
{
    public class FirestoreRepository
    {
        private readonly ILogger<FirestoreRepository> _logger;
        private FirestoreDb _db;
        private readonly IDistributedCache _cache;

        public FirestoreRepository(ILogger<FirestoreRepository> logger, IConfiguration config, IDistributedCache cache)
        {
            _logger = logger;

            string projectId = config["Authentication:Google:ProjectId"]!;
            string databaseId = config["Authentication:Google:DatabaseId"]!;

            var fb = new FirestoreDbBuilder
            {
                ProjectId = projectId,
                DatabaseId = databaseId

            };

            _db = fb.Build();
            _cache = cache;
        }


        public async Task AddTicket(TicketPost post)
        {
            // use the GUID in post.TicketId as the document ID
            var docRef = _db
                .Collection("posts")
                .Document(post.TicketId);

            await docRef.SetAsync(post);
            _logger.LogInformation($"Post {post.TicketId} added to Firestore as document ID");
        }



        public async Task<List<TicketPost>> GetTickets()
        {
            List<TicketPost> posts = new List<TicketPost>();
            Query allPostsQuery = _db.Collection(("posts"));
            QuerySnapshot allPostsQuerySnapshots = await allPostsQuery.GetSnapshotAsync();
            foreach (DocumentSnapshot document in allPostsQuerySnapshots)
            {
                TicketPost post = document.ConvertTo<TicketPost>();
                posts.Add(post);
            }
            _logger.LogInformation($"{posts.Count} loaded successfully");
            return posts;

        }

        public async Task UpdateTicketAsync(string ticketId, string supportAgent = null, bool? closed = null, DateTimeOffset? closeDate = null)
        {
            var docRef = _db.Collection("posts").Document(ticketId);
            var updates = new Dictionary<string, object>();
            if (supportAgent != null) updates["SupportAgent"] = supportAgent;
            if (closed.HasValue) updates["ClosedTicket"] = closed.Value;
            if (closeDate.HasValue) updates["CloseDate"] = closeDate.Value;
            if (updates.Count > 0)
            {
                await docRef.UpdateAsync(updates);
                _logger.LogInformation(
                    $"Ticket {ticketId} updated: " +
                    $"{(supportAgent != null ? $"SupportAgent={supportAgent} " : "")}" +
                    $"{(closed.HasValue ? $"ClosedTicket={closed} " : "")}" +
                    $"{(closeDate.HasValue ? $"CloseDate={closeDate}" : "")}"
                );
            }
        }


        internal async Task<TicketPost?> GetTicketByIdAsync(string ticketId)
        {
            // cache-first omitted for clarity…
            var doc = await _db
                .Collection("posts")  
                .Document(ticketId)
                .GetSnapshotAsync();

            if (!doc.Exists) return null;

            var ticket = doc.ConvertTo<TicketPost>();
            ticket.TicketId = doc.Id;
            return ticket;
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            var query = _db
                .Collection("users")
                .WhereEqualTo("Email", email)
                .Limit(1);

            var snapshot = await query.GetSnapshotAsync();
            var doc = snapshot.Documents.FirstOrDefault();
            if (doc == null) return null;

            var user = doc.ConvertTo<User>();
            user.Id = doc.Id;
            return user;
        }

        public async Task CreateUserAsync(User user)
        {
            var doc = _db.Collection("users").Document();
            await doc.SetAsync(user);
            user.Id = doc.Id;
        }


        public async Task<User?> GetUserByIdAsync(string userId)
        {
            var doc = await _db.Collection("users")
                               .Document(userId)
                               .GetSnapshotAsync();
            if (!doc.Exists) return null;
            var u = doc.ConvertTo<User>();
            u.Id = doc.Id;
            return u;
        }

        public async Task UpdateUserRoleAsync(string userId, string role)
        {
            await _db.Collection("users")
                     .Document(userId)
                     .UpdateAsync(new Dictionary<string, object>
                     {
                 { "Role", role }
                     });
        }


        public async Task ArchiveOldClosedTicketsAsync()
        {
            //Find all tickets closed more than 7 days ago
            var cutoff = DateTimeOffset.UtcNow.AddDays(-7);
            var query = _db.Collection("posts")
                           .WhereEqualTo("ClosedTicket", true)
                           .WhereLessThanOrEqualTo("CloseDate", cutoff);
            var snaps = await query.GetSnapshotAsync();

            if (snaps.Count == 0) return;

            // For each one, copy into "tickets-archive" and delete from "posts"
            foreach (var doc in snaps.Documents)
            {
                var ticket = doc.ConvertTo<TicketPost>();
                ticket.TicketId = doc.Id;

                // write into archive collection
                await _db.Collection("tickets-archive")
                         .Document(doc.Id)
                         .SetAsync(ticket);

                // remove from the live collection
                await doc.Reference.DeleteAsync();
                _logger.LogInformation($"Archived ticket {doc.Id} (closed before {cutoff})");
            }
        }



    }
}