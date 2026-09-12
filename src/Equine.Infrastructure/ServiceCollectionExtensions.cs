using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Equine.Infrastructure.Audit;
using Equine.Infrastructure.Services;
using Equine.Infrastructure.Storage;
using Equine.Infrastructure.Tenancy;

namespace Equine.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' not found.");

        services.AddDbContext<EquineDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
            }));

        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<TenantProvisioningService>();
        services.AddScoped<IAuditWriter, Audit.AuditWriter>();
        services.Configure<PractitionerOptions>(configuration.GetSection("Practitioner"));
        services.AddScoped<IPdfExportService, PdfExportService>();
        services.AddScoped<Equine.Infrastructure.Export.IArchiveExportService, Equine.Infrastructure.Export.ArchiveExportService>();

        services.Configure<ObjectStorageOptions>(configuration.GetSection("Storage"));
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var options = configuration.GetSection("Storage").Get<ObjectStorageOptions>() ?? new ObjectStorageOptions();
            var config = new AmazonS3Config
            {
                ServiceURL = options.ServiceUrl,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1"
            };
            return new AmazonS3Client(options.AccessKey, options.SecretKey, config);
        });
        services.AddSingleton<IObjectStorage, S3ObjectStorage>();

        return services;
    }
}
