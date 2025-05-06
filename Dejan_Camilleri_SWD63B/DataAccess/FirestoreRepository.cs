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

        //public async Task AddTicket(TicketPost post)
        //{
        //    await _db.Collection("posts").AddAsync(post);
        //    _logger.LogInformation($"Post {post.TicketId} added to Firestore");
        //}

        public async Task AddTicket(TicketPost post)
        {
            var docRef = _db.Collection("posts").Document(post.TicketId);
            await docRef.SetAsync(post);
            _logger.LogInformation($"Post {post.TicketId} added to Firestore (as document ID)");
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

        public async Task UpdateTicketAsync( string ticketId, string supportAgent = null, bool? closed = null)
        {
            var docRef = _db.Collection("posts").Document(ticketId);
            var updates = new Dictionary<string, object>();
            if (supportAgent != null) updates["SupportAgent"] = supportAgent;
            if (closed.HasValue) updates["ClosedTicket"] = closed.Value;
            if (updates.Count > 0)
            {
                await docRef.UpdateAsync(updates);
                _logger.LogInformation($"Ticket {ticketId} updated: " +
                    $"{(supportAgent != null ? $"SupportAgent={supportAgent} " : "")}" +
                    $"{(closed.HasValue ? $"ClosedTicket={closed}" : "")}");
            }
        }

        //internal async Task<TicketPost?> GetTicketByIdAsync(string ticketId)
        //{
        //    // 1) Try the cache first
        //    var cacheKey = $"Ticket:{ticketId}";
        //    var cachedJson = await _cache.GetStringAsync(cacheKey);
        //    if (!string.IsNullOrEmpty(cachedJson))
        //    {
        //        // deserialize and return
        //        var cachedTicket = JsonSerializer.Deserialize<TicketPost>(cachedJson);
        //        return cachedTicket;
        //    }

        //    // 2) Cache-miss → hit Firestore
        //    var doc = await _db
        //        .Collection("tickets")
        //        .Document(ticketId)
        //        .GetSnapshotAsync();

        //    if (!doc.Exists)
        //        return null;

        //    var ticket = doc.ConvertTo<TicketPost>();
        //    ticket.TicketId = doc.Id;

        //    // 3) Store in Redis for future requests (e.g. 7-day sliding/window)
        //    var json = JsonSerializer.Serialize(ticket);
        //    var opts = new DistributedCacheEntryOptions
        //    {
        //        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
        //    };
        //    await _cache.SetStringAsync(cacheKey, json, opts);

        //    return ticket;
        //}

        internal async Task<TicketPost?> GetTicketByIdAsync(string ticketId)
        {
            // cache-first omitted for clarity…
            var doc = await _db
                .Collection("posts")   // match your Firestore UI!
                .Document(ticketId)
                .GetSnapshotAsync();

            if (!doc.Exists) return null;

            var ticket = doc.ConvertTo<TicketPost>();
            ticket.TicketId = doc.Id;
            return ticket;
        }



    }
}
