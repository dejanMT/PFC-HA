using Dejan_Camilleri_SWD63B.Interfaces;
using Dejan_Camilleri_SWD63B.Models;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Dejan_Camilleri_SWD63B.Services
{
    public class RedisCacheService : ICacheService
    {
        private const string Key = "cached_tickets";
        private readonly IDistributedCache _cache;
        private readonly JsonSerializerOptions _opts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public RedisCacheService(IDistributedCache cache)
        {
            _cache = cache;
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
    }
}
