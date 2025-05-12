namespace Dejan_Camilleri_SWD63B.Interfaces
{
    public interface IMailService
    {
        Task SendEmailAsync(string to, string subject, string body);
    }
}
