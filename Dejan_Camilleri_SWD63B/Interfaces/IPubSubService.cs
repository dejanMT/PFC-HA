namespace Dejan_Camilleri_SWD63B.Interfaces
{
    public interface IPubSubService
    {
        Task PublishTicketAsync(object payload, string priority);
    }
}
