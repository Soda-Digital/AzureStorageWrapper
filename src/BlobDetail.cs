
namespace Soda.Storage
{
    public class BlobDetail
    {
        public BlobDetail(string url, long contentLength, string contentType)
        {
            Url = url;
            ContentLength = contentLength;
            ContentType = contentType;
        }

        public string Url { get; }
        public long ContentLength { get; }
        public string ContentType { get; }
    }

}