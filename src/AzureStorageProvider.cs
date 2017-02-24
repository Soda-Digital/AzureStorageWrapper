using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Soda.Storage
{
    public class AzureStorageProvider
    {

        private IDictionary<string, CloudBlobContainer> initialisedContainers = new Dictionary<string, CloudBlobContainer>();

        private readonly CloudBlobClient _blobClient;
        private readonly BlobContainerPublicAccessType _defaultContainerAccessType;

        public AzureStorageProvider(string connectionString, BlobContainerPublicAccessType defaultContainerAccessType = BlobContainerPublicAccessType.Off)
        {
            _defaultContainerAccessType = defaultContainerAccessType;
            CloudStorageAccount _storageAccount;
            if (string.IsNullOrEmpty(connectionString))
            {
                _storageAccount = CloudStorageAccount.DevelopmentStorageAccount;
            }
            else
            {
                _storageAccount = CloudStorageAccount.Parse(connectionString);
            }
            _blobClient = _storageAccount.CreateCloudBlobClient();
        }

        public async Task<string> Upload(Stream resource, string reference, string containerName, string contentType = null)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }
            if (string.IsNullOrEmpty(reference))
            {
                throw new ArgumentNullException(nameof(reference));
            }
            if (string.IsNullOrEmpty(containerName))
            {
                throw new ArgumentNullException(nameof(containerName));
            }

            CloudBlobContainer container;
            if (!initialisedContainers.TryGetValue(containerName, out container))
            {
                container = await initialiseContainer(containerName);
            }

            var blockBlob = container.GetBlockBlobReference(reference);
            await blockBlob.UploadFromStreamAsync(resource);

            if (!string.IsNullOrEmpty(contentType))
            {
                blockBlob.Properties.ContentType = contentType;
                await blockBlob.SetPropertiesAsync();
            }

            return blockBlob.Uri.ToString();
        }

        public async Task<string> Upload(byte[] resource, string reference, string containerName, string contentType = null)
        {
            using (var ms = new MemoryStream(resource))
            {
                return await Upload(ms, reference, containerName, contentType);
            }
        }

        private async Task<CloudBlobContainer> initialiseContainer(string containerName)
        {
            var container = _blobClient
                .GetContainerReference(containerName);

            await container.CreateIfNotExistsAsync(_defaultContainerAccessType, null, null);

            return container;

        }
    }
}
