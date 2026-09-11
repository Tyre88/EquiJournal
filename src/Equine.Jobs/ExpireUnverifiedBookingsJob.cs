using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Equine.Jobs;

public sealed class ExpireUnverifiedBookingsJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ExpireUnverifiedBookingsJob> _log;

    public ExpireUnverifiedBookingsJob(IServiceScopeFactory scopes, ILogger<ExpireUnverifiedBookingsJob> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ExpireUnverifiedBookingsService>();
                await service.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Failed to expire unverified public bookings");
            }
        }
    }
}
