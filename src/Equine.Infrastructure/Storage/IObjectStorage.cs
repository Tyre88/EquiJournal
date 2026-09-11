namespace Equine.Infrastructure.Storage;

public interface IObjectStorage
{
    Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default);
    string GetPresignedUrl(string key, TimeSpan expiresIn);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}

public class ObjectStorageOptions
{
    public string ServiceUrl { get; set; } = "http://localhost:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public string Bucket { get; set; } = "equine-attachments";
    public string? PublicBaseUrl { get; set; }
}
