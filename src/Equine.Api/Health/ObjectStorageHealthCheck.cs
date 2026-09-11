using Equine.Infrastructure.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Equine.Api.Health;

public sealed class ObjectStorageHealthCheck : IHealthCheck
{
    private readonly IObjectStorage _storage;
    private readonly ObjectStorageOptions _options;

    public ObjectStorageHealthCheck(IObjectStorage storage, IOptions<ObjectStorageOptions> options)
    {
        _storage = storage;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _storage.ExistsAsync("__healthcheck__", cancellationToken);
            return HealthCheckResult.Healthy(_options.Bucket);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Object storage unreachable", ex);
        }
    }
}
