﻿using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Soda.Storage
{
    public static class Extensions
    {
        public static Task<string> Upload(this AzureBlobStorageProvider storage, IFormFile file, string reference, string containerName = null) => storage.Upload(file.OpenReadStream(), reference, containerName, file.ContentType);
    }
}
