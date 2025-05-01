using Dejan_Camilleri_SWD63B.Models;
using Google.Cloud.Firestore;

namespace Dejan_Camilleri_SWD63B.DataAccess
{
    public class FirestoreRepository
    {
        private readonly ILogger<FirestoreRepository> _logger;
        private FirestoreDb _db;

        public FirestoreRepository(ILogger<FirestoreRepository> logger, IConfiguration config)
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
        }

        public async Task AddTicket(TicketPost post)
        {
            await _db.Collection("posts").AddAsync(post);
            _logger.LogInformation($"Post {post.TicketId} added to Firestore");
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


    }
}
