using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;                        // for AsEnumerable()
using System.Net.Http;                   // for HttpMethod
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using StorageObject = Google.Apis.Storage.v1.Data.Object;   // ← same alias
using Dejan_Camilleri_SWD63B.Interfaces;

namespace Dejan_Camilleri_SWD63B.Services
{
    public class FileUploadService : IFileUploadService
    {
        private readonly ICloudLoggingService _logger;
        private readonly StorageClient _storageClient;
        private readonly string _bucketName;
        private readonly UrlSigner _signer;

        public FileUploadService(
            ICloudLoggingService logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _bucketName = configuration["Authentication:Google:BucketName"];

            var credPath = configuration["Authentication:Google:ServiceAccountCredentials"];
            var credentials = GoogleCredential.FromFile(credPath);
            _storageClient = StorageClient.Create(credentials);
            _signer = UrlSigner.FromServiceAccountPath(credPath);
        }

        public async Task<string> UploadFileAsync(IFormFile file, string fileName)
        {
            if (!file.ContentType.StartsWith("image/"))
                throw new InvalidOperationException("Only image files are allowed.");

            fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            ms.Position = 0;

            await _logger.LogInformationAsync($"Uploading {fileName} to {_bucketName}");
            await _storageClient.UploadObjectAsync(_bucketName, fileName, file.ContentType, ms);

            var publicUrl = $"https://storage.googleapis.com/{_bucketName}/{fileName}";
            await _logger.LogInformationAsync($"Uploaded → {publicUrl}");
            return publicUrl;
        }

        public async Task DeletePostImageAsync(string imageUrl)
        {
            var name = Path.GetFileName(new Uri(imageUrl).AbsolutePath);
            await _storageClient.DeleteObjectAsync(_bucketName, name);
            await _logger.LogInformationAsync($"Deleted {name} from {_bucketName}");
        }

        public Task<IEnumerable<StorageObject>> ListObjectsAsync(string prefix)
        {
            // StorageClient.ListObjects returns IEnumerable<StorageObject>
            var blobs = _storageClient.ListObjects(_bucketName, prefix);
            return Task.FromResult(blobs.AsEnumerable());
        }

        public Task<string> GetSignedUrlAsync(string objectName, TimeSpan expiry)
        {
            // produce a time-limited V4 GET URL
            return _signer.SignAsync(_bucketName, objectName, expiry, HttpMethod.Get);
        }
    }
}
