using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Equine.Infrastructure.Storage;

public sealed class S3ObjectStorage : IObjectStorage
{
    private readonly IAmazonS3 _s3;
    private readonly ObjectStorageOptions _options;

    public S3ObjectStorage(IAmazonS3 s3, IOptions<ObjectStorageOptions> options)
    {
        _s3 = s3;
        _options = options.Value;
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        };
        await _s3.PutObjectAsync(request, cancellationToken);
    }

    public async Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var response = await _s3.GetObjectAsync(_options.Bucket, key, cancellationToken);
        return response.ResponseStream;
    }

    public string GetPresignedUrl(string key, TimeSpan expiresIn)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Expires = DateTime.UtcNow.Add(expiresIn),
            Verb = HttpVerb.GET
        };
        return _s3.GetPreSignedURL(request);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _s3.GetObjectMetadataAsync(_options.Bucket, key, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
