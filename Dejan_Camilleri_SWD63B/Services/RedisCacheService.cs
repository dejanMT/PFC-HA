using Dejan_Camilleri_SWD63B.Interfaces;
using Dejan_Camilleri_SWD63B.DataAccess;
using Dejan_Camilleri_SWD63B.Models;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Dejan_Camilleri_SWD63B.Services
{
    public class RedisCacheService : ICacheService
    {
        private const string Key = "cached_tickets";
        private readonly IDistributedCache _cache;
        private readonly FirestoreRepository _firestoreRepo;
        private readonly JsonSerializerOptions _opts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public RedisCacheService(IDistributedCache cache, FirestoreRepository firestoreRepo)
        {
            _cache = cache;
            _firestoreRepo = firestoreRepo;
        }

        public async Task<List<TicketPost>> GetTicketsAsync()
        {
            var data = await _cache.GetStringAsync(Key);
            return data is null
                 ? new List<TicketPost>()
                 : JsonSerializer.Deserialize<List<TicketPost>>(data, _opts);
        }

        public async Task SetTicketsAsync(IEnumerable<TicketPost> tickets)
        {
            var json = JsonSerializer.Serialize(tickets, _opts);
            var opts = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            };
            await _cache.SetStringAsync(Key, json, opts);
        }

        public async Task RemoveTicketAsync(string ticketId)
        {
            var list = await GetTicketsAsync();
            var filtered = list.Where(t => t.TicketId != ticketId).ToList();
            await SetTicketsAsync(filtered);
        }

        public async Task SetTicketAsync(TicketPost ticket)
        {
            var all = await GetTicketsAsync();
            var existing = all.FirstOrDefault(x => x.TicketId == ticket.TicketId);
            if (existing != null) all.Remove(existing);
            all.Add(ticket);
            await SetTicketsAsync(all);
        }

        public async Task<List<string>> GetTechnicianEmailsAsync()
        {
            var users = await _firestoreRepo.GetAllUsersAsync();
            return users
              .Where(u => u.Role == "Technician")
              .Select(u => u.Email)
              .ToList();
        }

        public void RemoveTickets()
      => _cache.Remove(Key);


    }
}