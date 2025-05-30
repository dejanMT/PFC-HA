﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using StorageObject = Google.Apis.Storage.v1.Data.Object;  

namespace Dejan_Camilleri_SWD63B.Interfaces
{
    public interface IFileUploadService
    {


        //Task<string> UploadFileAsync(IFormFile file, string fileName);
        //Task<string> UploadFileAsync(IFormFile file, string uploaderEmail);
        Task<string> UploadFileAsync(IFormFile file, string fileName, string uploaderEmail);
        Task DeletePostImageAsync(string imageUrl);
        Task<IEnumerable<StorageObject>> ListObjectsAsync(string prefix);
        Task<string> GetSignedUrlAsync(string objectName, TimeSpan expiry);
    }
}
