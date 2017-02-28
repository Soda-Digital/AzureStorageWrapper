using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Net.Http;

namespace Soda.Storage
{
    public class AzureBlobStorageProvider
    {
        private IDictionary<string, CloudBlobContainer> initialisedContainers = new Dictionary<string, CloudBlobContainer>();
        private readonly CloudBlobClient _blobClient;
        private readonly string _defaultContainer;
        private readonly BlobContainerPublicAccessType _defaultContainerAccessType;

        public AzureBlobStorageProvider(string connectionString, string defaultContainer = null, BlobContainerPublicAccessType defaultContainerAccessType = BlobContainerPublicAccessType.Off)
        {
            _defaultContainerAccessType = defaultContainerAccessType;
            _defaultContainer = defaultContainer;
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

        public async Task<string> Upload(Stream resource, string reference, string containerName = null, string contentType = null)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }
            if (string.IsNullOrEmpty(reference))
            {
                throw new ArgumentNullException(nameof(reference));
            }
            if (string.IsNullOrEmpty(_defaultContainer) && string.IsNullOrEmpty(containerName))
            {
                throw new ArgumentNullException(nameof(containerName));
            }

            var container = await GetOrCreateContainer(containerName);

            var blockBlob = container.GetBlockBlobReference(reference);
            await blockBlob.UploadFromStreamAsync(resource);

            if (!string.IsNullOrEmpty(contentType))
            {
                blockBlob.Properties.ContentType = contentType;
                await blockBlob.SetPropertiesAsync();
            }

            //note this doesn't actually work
            return blockBlob.Uri.ToString();
        }

        public async Task<string> Upload(byte[] resource, string reference, string containerName = null, string contentType = null)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            using (var ms = new MemoryStream(resource))
            {
                return await Upload(ms, reference, containerName, contentType);
            }
        }

        public async Task<Stream> StreamResource(string resource, string containerName)
        {
            var container = await GetOrCreateContainer(containerName);

            var blockBlob = container.GetBlockBlobReference(resource);

            var ms = new MemoryStream();
            await blockBlob.DownloadToStreamAsync(ms);
            
            return ms;

        }

        public async Task<string> BlobUrl(string resource, string containerName = null)
        {
            if (string.IsNullOrEmpty(resource))
            {
                throw new ArgumentNullException(nameof(resource));
            }
            var container = await GetOrCreateContainer(containerName);
            var blockBlob = container.GetBlockBlobReference(resource);

            if (container.Properties.PublicAccess == BlobContainerPublicAccessType.Off)
            {
                var policy = new SharedAccessBlobPolicy()
                {
                    Permissions = SharedAccessBlobPermissions.Read,
                    SharedAccessExpiryTime = DateTime.UtcNow.AddMinutes(1)
                };
                var sas = blockBlob.GetSharedAccessSignature(policy);
                //note, this doesn't work.
                
                blockBlob.Uri.Query.Insert(0, sas);
            }

            return blockBlob.Uri.ToString();

        }

        private async Task<CloudBlobContainer> GetOrCreateContainer(string containerName)
        {
            if (string.IsNullOrEmpty(_defaultContainer) && string.IsNullOrEmpty(containerName))
            {
                throw new ArgumentNullException(nameof(containerName));
            }
            if (string.IsNullOrEmpty(containerName))
            {
                containerName = _defaultContainer;
            }
            CloudBlobContainer container;
            if (!initialisedContainers.TryGetValue(containerName, out container))
            {
                await container.CreateIfNotExistsAsync(_defaultContainerAccessType, null, null);
                //preload container permissions.
                await container.GetPermissionsAsync();
            }

            return container;
        }

      
    }
}
