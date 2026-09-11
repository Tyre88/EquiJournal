using System.Collections.Concurrent;

namespace Equine.Infrastructure.Storage;

public sealed class MemoryObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, (byte[] Bytes, string ContentType)> _objects = new();

    public Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        _objects[key] = (ms.ToArray(), contentType);
        return Task.CompletedTask;
    }

    public Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_objects.TryGetValue(key, out var item))
            throw new FileNotFoundException(key);
        return Task.FromResult<Stream>(new MemoryStream(item.Bytes, writable: false));
    }

    public string GetPresignedUrl(string key, TimeSpan expiresIn) => $"memory://{key}?exp={expiresIn.TotalSeconds}";

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_objects.ContainsKey(key));
}
