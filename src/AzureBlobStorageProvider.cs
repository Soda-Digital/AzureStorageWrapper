using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Soda.Storage
{
    public class AzureBlobStorageProvider
    {
        private const string PRIVATE_CACHE_CONTROL = "private";

        private ConcurrentDictionary<string, CloudBlobContainer> initialisedContainers = new ConcurrentDictionary<string, CloudBlobContainer>();
        private readonly CloudBlobClient _blobClient;
        private readonly string _defaultContainer;
        private readonly BlobContainerPublicAccessType _defaultContainerAccessType;

        public Func<string> FileNameGenerator { get => _fileNameGenerator; set => _fileNameGenerator = value; }
        private Func<string> _fileNameGenerator = () => Guid.NewGuid().ToString();


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
            
            if (container.Properties.PublicAccess == BlobContainerPublicAccessType.Off)
            {
                blockBlob.Properties.CacheControl = PRIVATE_CACHE_CONTROL;
            }
            if (!string.IsNullOrEmpty(contentType))
            {
                blockBlob.Properties.ContentType = contentType;
                await blockBlob.SetPropertiesAsync();
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
                return await Upload(ms, reference, containerName, contentType);
            }
        }

        public async Task<Stream> StreamResource(string resource, string containerName = null)
        {
            var container = await GetOrCreateContainer(containerName);

            var blockBlob = container.GetBlockBlobReference(resource);

            var ms = new MemoryStream();
            await blockBlob.DownloadToStreamAsync(ms);
            ms.Position = 0;

            return ms;

        }

        public async Task DeleteResource(string resource, string containerName = null)
        {
            var container = await GetOrCreateContainer(containerName);
            var blockBlob = container.GetBlockBlobReference(resource);
            await blockBlob.DeleteIfExistsAsync();            
        }

        public async Task<string> BlobUrl(string resource, DateTime sasTimeout, string containerName = null)
        {
            if (string.IsNullOrEmpty(resource))
            {
                throw new ArgumentNullException(nameof(resource));
            }
            var container = await GetOrCreateContainer(containerName);
            var blockBlob = container.GetBlockBlobReference(resource);

            var returnUri = blockBlob.Uri;

            if (container.Properties.PublicAccess == BlobContainerPublicAccessType.Off)
            {
                var policy = new SharedAccessBlobPolicy()
                {
                    Permissions = SharedAccessBlobPermissions.Read,
                    SharedAccessExpiryTime = sasTimeout
                };
                var sas = blockBlob.GetSharedAccessSignature(policy);

                Uri concatedUri;
                if (Uri.TryCreate(returnUri, sas, out concatedUri))
                {
                    return concatedUri.ToString();
                }
                
            }

            return returnUri.ToString();

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
                container = _blobClient.GetContainerReference(containerName);
                await container.CreateIfNotExistsAsync(_defaultContainerAccessType, null, null);
                //preload container permissions.
                await container.GetPermissionsAsync();
                //add to list of initialise containers.
                initialisedContainers.TryAdd(containerName, container);
            }

            return container;
        }
    }
}
