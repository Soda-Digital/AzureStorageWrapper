using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Soda.Storage
{
    internal class LockedContainer : CloudBlobContainer
    {
        public LockedContainer(Uri containerAddress) : base(containerAddress)
        {
        }

        public LockedContainer(Uri containerAddress, StorageCredentials credentials) : base(containerAddress, credentials)
        {
        }

        public LockedContainer(StorageUri containerAddress, StorageCredentials credentials) : base(containerAddress, credentials)
        {
        }
    }

    public class AzureBlobStorageProvider
    {
        private readonly static ConcurrentDictionary<string, CloudBlobContainer> _initialisedContainers = new ConcurrentDictionary<string, CloudBlobContainer>();
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

            var container = await GetOrCreateContainer(containerName).ConfigureAwait(false);

            var blockBlob = container.GetBlockBlobReference(reference);
            await blockBlob.UploadFromStreamAsync(resource).ConfigureAwait(false);

            if (container.Properties.PublicAccess == BlobContainerPublicAccessType.Off)
            {
                blockBlob.Properties.CacheControl = "private";
            }
            if (!string.IsNullOrEmpty(contentType))
            {
                blockBlob.Properties.ContentType = contentType;
                await blockBlob.SetPropertiesAsync().ConfigureAwait(false);
            }

            return reference;
        }

        public async Task<string> Upload(byte[] resource, string reference, string containerName = null, string contentType = null)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            using (var ms = new MemoryStream(resource))
            {
                return await Upload(ms, reference, containerName, contentType).ConfigureAwait(false);
            }
        }

        public async Task<Stream> StreamResource(string resource, string containerName = null)
        {
            if (string.IsNullOrEmpty(resource))
            {
                throw new ArgumentNullException(nameof(resource));
            }

            var container = await GetOrCreateContainer(containerName).ConfigureAwait(false);

            var blockBlob = container.GetBlockBlobReference(resource);

            var ms = new MemoryStream();
            await blockBlob.DownloadToStreamAsync(ms).ConfigureAwait(false);
            ms.Position = 0;

            return ms;
        }

        public async Task DeleteResource(string resource, string containerName = null)
        {
            if (string.IsNullOrEmpty(resource))
            {
                throw new ArgumentNullException(nameof(resource));
            }

            var container = await GetOrCreateContainer(containerName).ConfigureAwait(false);
            var blockBlob = container.GetBlockBlobReference(resource);
            await blockBlob.DeleteIfExistsAsync().ConfigureAwait(false);
        }

        public async Task<string> BlobUrl(string resource, DateTime sasTimeout, string containerName = null)
        {
            if (string.IsNullOrEmpty(resource))
            {
                throw new ArgumentNullException(nameof(resource));
            }
            var container = await GetOrCreateContainer(containerName).ConfigureAwait(false);
            var blockBlob = container.GetBlockBlobReference(resource);

            var returnUri = blockBlob.Uri.ToString();

            if (container.Properties.PublicAccess == BlobContainerPublicAccessType.Off)
            {
                var policy = new SharedAccessBlobPolicy()
                {
                    Permissions = SharedAccessBlobPermissions.Read,
                    SharedAccessExpiryTime = sasTimeout
                };
                var sas = blockBlob.GetSharedAccessSignature(policy);
                //todo better url concat
                returnUri += sas;
            }

            return returnUri;
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

            if (!_initialisedContainers.TryGetValue(containerName, out var container))
            {
                //get a reference to the container
                container = _blobClient.GetContainerReference(containerName);
                //create if it doesn't exist
                await container.CreateIfNotExistsAsync(_defaultContainerAccessType, null, null).ConfigureAwait(false);
                //preload container permissions.
                await container.GetPermissionsAsync().ConfigureAwait(false);
                //add to list of initialise containers.
                _initialisedContainers.TryAdd(containerName, container);
            }

            return container;
        }
    }
}
