using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Collections.Generic;

namespace Soda.Storage
{
    public class AzureBlobStorageProvider
    {
        private readonly IDictionary<string, CloudBlobContainer> _cachedContainers = new Dictionary<string, CloudBlobContainer>();
        private readonly CloudBlobClient _blobClient;

        public AzureBlobStorageProvider(string connectionString)
        {
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

        public async Task<string> Upload(Stream resource, string reference, string containerName, string contentType, string cacheControl = "private")
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

            var container = _blobClient.GetContainerReference(containerName);

            var blockBlob = container.GetBlockBlobReference(reference);
            await blockBlob.UploadFromStreamAsync(resource).ConfigureAwait(false);

            blockBlob.Properties.CacheControl = cacheControl;
            blockBlob.Properties.ContentType = contentType;

            await blockBlob.SetPropertiesAsync().ConfigureAwait(false);

            return reference;
        }

        public async Task<string> Upload(byte[] resource, string reference, string containerName, string contentType, string cacheControl = "private")
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            using (var ms = new MemoryStream(resource))
            {
                return await Upload(ms, reference, containerName, contentType, cacheControl).ConfigureAwait(false);
            }
        }

        public async Task<Stream> StreamResource(string resource, string containerName)
        {
            if (string.IsNullOrEmpty(resource))
            {
                throw new ArgumentNullException(nameof(resource));
            }
            if (string.IsNullOrEmpty(containerName))
            {
                throw new ArgumentNullException(nameof(containerName));
            }

            var container = _blobClient.GetContainerReference(containerName);

            var blockBlob = container.GetBlockBlobReference(resource);

            var ms = new MemoryStream();
            await blockBlob.DownloadToStreamAsync(ms).ConfigureAwait(false);
            ms.Position = 0;

            return ms;
        }

        public async Task DeleteResource(string resource, string containerName)
        {
            if (string.IsNullOrEmpty(resource))
            {
                throw new ArgumentNullException(nameof(resource));
            }
            if (string.IsNullOrEmpty(containerName))
            {
                throw new ArgumentNullException(nameof(containerName));
            }

            var container = _blobClient.GetContainerReference(containerName);
            var blockBlob = container.GetBlockBlobReference(resource);
            await blockBlob.DeleteIfExistsAsync().ConfigureAwait(false);
        }

        public string BlobUrl(string resource, string containerName)
        {
            if (string.IsNullOrEmpty(resource))
            {
                throw new ArgumentNullException(nameof(resource));
            }
            if (string.IsNullOrEmpty(containerName))
            {
                throw new ArgumentNullException(nameof(containerName));
            }
            var container = _blobClient.GetContainerReference(containerName);
            var blockBlob = container.GetBlockBlobReference(resource);

            return blockBlob.Uri.ToString();
        }

        public string BlobUrl(string resource, string containerName, DateTime sasTimeout)
        {
            if (string.IsNullOrEmpty(resource))
            {
                throw new ArgumentNullException(nameof(resource));
            }
            if (string.IsNullOrEmpty(containerName))
            {
                throw new ArgumentNullException(nameof(containerName));
            }
            var container = _blobClient.GetContainerReference(containerName);
            var blockBlob = container.GetBlockBlobReference(resource);

            var returnUri = blockBlob.Uri.ToString();
            var policy = new SharedAccessBlobPolicy()
            {
                Permissions = SharedAccessBlobPermissions.Read,
                SharedAccessExpiryTime = sasTimeout
            };
            var sas = blockBlob.GetSharedAccessSignature(policy);
            //todo better url concat
            returnUri += sas;

            return returnUri;
        }

        public async Task InitializeContainer(string containerName, BlobContainerPublicAccessType containerPublicAccessType)
        {
            var container = _blobClient.GetContainerReference(containerName);
            await container.CreateIfNotExistsAsync(containerPublicAccessType, null, null).ConfigureAwait(false);
        }
    }
}
