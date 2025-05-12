using Dejan_Camilleri_SWD63B.Models;

namespace Dejan_Camilleri_SWD63B.Interfaces
{
    public interface ICacheService
    {
        Task<List<TicketPost>> GetTicketsAsync();
        Task SetTicketsAsync(IEnumerable<TicketPost> tickets);
        Task RemoveTicketAsync(string ticketId);


        Task SetTicketAsync(TicketPost ticket);
        Task<List<string>> GetTechnicianEmailsAsync();
    }
}
