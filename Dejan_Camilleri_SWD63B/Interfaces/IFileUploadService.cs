namespace Dejan_Camilleri_SWD63B.Interfaces
{
    public interface IFileUploadService
    {
        Task<string> UploadFileAsync(IFormFile file, string fileName);
    }
}
