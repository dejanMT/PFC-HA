namespace Dejan_Camilleri_SWD63B.Interfaces
{
    public interface IFileUploadService
    {
        Task<string> UploadFileAsync(IFormFile file, string fileName);

        Task<IEnumerable<Google.Apis.Storage.v1.Data.Object>> ListObjectsAsync(string prefix);

        Task<string> GetSignedUrlAsync(string objectName, TimeSpan validFor);
    }
}
