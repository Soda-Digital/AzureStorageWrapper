using Soda.Storage;
using System;
using System.Threading.Tasks;
using Xunit;

namespace AzureBlobStorageProvider.Tests
{
    public class UnitTest1
    {
        [Fact]
        public async Task GetBlobUrl()
        {
            var client = await new Soda.Storage.AzureBlobStorageProvider(string.Empty).InitializeContainers(Microsoft.WindowsAzure.Storage.Blob.BlobContainerPublicAccessType.Blob, "wtf", "foobar");

            var url = client.BlobUrl("catjpg", "wtf", DateTime.Now);

            var url2 = client.BlobUrl("catjpg", "foobar");

            Assert.Equal("catjpg", url);
        }
    }
}
