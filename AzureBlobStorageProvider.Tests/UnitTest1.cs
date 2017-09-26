using System;
using Xunit;

namespace AzureBlobStorageProvider.Tests
{
    public class UnitTest
    {
        [Fact]
        public void GetBlobUrl()
        {
            var client = new Soda.Storage.AzureBlobStorageProvider(string.Empty);

            var url = client.BlobUrl("catjpg", "wtf", DateTime.Now);

            var url2 = client.BlobUrl("catjpg", "foobar");

            Assert.Equal("catjpg", url);
        }
    }
}
