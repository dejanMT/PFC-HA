using Dejan_Camilleri_SWD63B.Interfaces;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Storage.v1.Data;
using Google.Cloud.Storage.V1;
using StackExchange.Redis;
using System.Xml.Linq;
using static Grpc.Core.Metadata;


namespace Dejan_Camilleri_SWD63B.Services
{
    public class FileUploadService : IFileUploadService
    {
        private readonly ICloudLoggingService _logger;
        private readonly StorageClient _storageClient;
        private readonly string _bucketName;
        private readonly UrlSigner _signer;
        private readonly TechnicianService _technicianService;

        public FileUploadService(ICloudLoggingService logger, TechnicianService technicianService)
        {
            _logger = logger;
            _bucketName = Environment.GetEnvironmentVariable("BucketName")
                          ?? throw new InvalidOperationException("BUCKET_NAME must be set");

            // Check for a mounted JSON key (local dev)
            var keyPath = Environment.GetEnvironmentVariable("ServiceAccountCredentials");

            if (!string.IsNullOrEmpty(keyPath))
            {
                // Local dev: use JSON key for both client & signer
                var cred = GoogleCredential.FromFile(keyPath);
                _storageClient = StorageClient.Create(cred);
                _signer = UrlSigner.FromServiceAccountPath(keyPath);
            }
            else
            {
                // Cloud Run: use ADC + IAM signing (no key file)
                var cred = GoogleCredential.GetApplicationDefault();
                _storageClient = StorageClient.Create(cred);
                _signer = UrlSigner.FromCredential(cred);
            }

            _technicianService = technicianService;

        }

        //public async Task<string> UploadFileAsync(IFormFile file, string fileName)
        //{
        //    try
        //    {
        //        if (!file.ContentType.StartsWith("image/"))
        //        {
        //            await _logger.LogWarningAsync($"Invalid file type: {file.ContentType}. Only images are allowed.");
        //            throw new InvalidOperationException("Only image files are allowed.");
        //        }

        //        // fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        //        var objectName = string.IsNullOrEmpty(fileName) ? $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}" : fileName;

        //        await _logger.LogInformationAsync($"Uploading file {fileName} to bucket {_bucketName}");

        //        // Create a memory stream to read the file
        //        using var memoryStream = new MemoryStream();
        //        await file.CopyToAsync(memoryStream);
        //        memoryStream.Position = 0;

        //        // Upload the file to Google Cloud Storage
        //        var uploadedObject = await _storageClient.UploadObjectAsync(
        //            bucket: _bucketName,
        //            objectName: objectName,
        //            contentType: file.ContentType,
        //            source: memoryStream

        //        );


        //        // Generate the public URL for the file
        //        string publicUrl = $"https://storage.cloud.google.com/{_bucketName}/{fileName}?authuser=1";

        //        await _logger.LogInformationAsync($"File uploaded successfully. Public URL: {publicUrl}");
        //        return objectName;
        //    }
        //    catch (Google.GoogleApiException gex)
        //    {
        //        await _logger.LogErrorAsync("Google Cloud Storage API error", gex);
        //        throw new ApplicationException("Cloud storage service error occurred.", gex);
        //    }
        //    catch (IOException ioex)
        //    {
        //        await _logger.LogErrorAsync("IO error while uploading file", ioex);
        //        throw new ApplicationException("Failed to read or process file data.", ioex);
        //    }
        //    catch (ArgumentException aex)
        //    {
        //        await _logger.LogErrorAsync("Invalid argument for file upload", aex);
        //        throw new ApplicationException("Invalid file upload parameters.", aex);
        //    }
        //    catch (Exception ex)
        //    {
        //        await _logger.LogErrorAsync("Unexpected error uploading file", ex);
        //        throw new ApplicationException("An unexpected error occurred during file upload.", ex);
        //    }
        //}

        /// <summary>
        /// Uploads a file to Google Cloud Storage and sets the ACL for the uploader and technicians.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="fileName"></param>
        /// <param name="uploaderEmail"></param>
        /// <returns></returns>
        public async Task<string> UploadFileAsync(IFormFile file, string fileName, string uploaderEmail)
        {
            var objectName = string.IsNullOrEmpty(fileName)
                ? $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}"
                : fileName;

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            ms.Position = 0;

            await _storageClient.UploadObjectAsync(
                bucket: _bucketName,
                objectName: objectName,
                contentType: file.ContentType,
                source: ms
            );

            var gcsObject = await _storageClient.GetObjectAsync(_bucketName, objectName);

            // Initial ACL with uploader
            gcsObject.Acl = new List<ObjectAccessControl>
            {
                new ObjectAccessControl
                {
                    Entity = $"user-{uploaderEmail}",
                    Role = "READER"
                }
            };

            // Dynamically fetch technician emails from Firestore
            var technicianEmails = await _technicianService.GetTechnicianEmailsAsync();

            foreach (var tech in technicianEmails)
            {
                gcsObject.Acl.Add(new ObjectAccessControl
                {
                    Entity = $"user-{tech}",
                    Role = "READER"
                });
            }

            await _storageClient.UpdateObjectAsync(gcsObject);

            return objectName;
        }

        /// <summary>
        /// Deletes a post image from Google Cloud Storage.
        /// </summary>
        /// <param name="imageUrl"></param>
        /// <returns></returns>
        public async Task DeletePostImageAsync(string imageUrl)
        {
            try
            {
                string fileName = imageUrl.Substring(imageUrl.LastIndexOf('/') + 1);
                await _storageClient.DeleteObjectAsync(_bucketName, fileName);
                await _logger.LogInformationAsync($"Post image {fileName} was successfully deleted from bucket {_bucketName}");
            }
            catch (Google.GoogleApiException ex)
            {
                await _logger.LogErrorAsync("Google Cloud Storage error while deleting image", ex);
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync("Unexpected error while deleting post image", ex);
            }
        }

        /// <summary>
        /// Lists objects in the specified bucket with the given prefix.
        /// </summary>
        /// <param name="prefix"></param>
        /// <returns></returns>
        public async Task<IEnumerable<Google.Apis.Storage.v1.Data.Object>> ListObjectsAsync(string prefix)
        {
            return _storageClient.ListObjects(_bucketName, prefix);
        }

        /// <summary>
        /// Generates a signed URL for the specified object in the bucket.
        /// </summary>
        /// <param name="objectName"></param>
        /// <param name="validFor"></param>
        /// <returns></returns>
        public async Task<string> GetSignedUrlAsync(string objectName, TimeSpan validFor)
        {
            return await _signer.SignAsync(_bucketName, objectName, validFor, HttpMethod.Get);
        }

       
    }
}