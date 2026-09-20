using Equine.Api;
using Equine.Api.Auth;
using Equine.Api.Endpoints;
using Equine.Api.Middleware;
using Equine.Api.Features.Owners;
using Equine.Api.Features.Horses;
using Equine.Api.Features.TreatmentTypes;
using Equine.Api.Features.Journals;
using Equine.Api.Features.Attachments;
using Equine.Api.Features.Availability;
using Equine.Api.Features.Bookings;
using Equine.Api.Features.Schema;
using Equine.Api.Features.PublicBookings;
using Equine.Api.Features.Settings;
using Equine.Api.Features.Notifications;
using Equine.Api.Features.FollowUps;
using Equine.Api.Features.Marketing;
using Equine.Api.Features.Portal;
using Equine.Api.Features.Reports;
using Equine.Api.Features.Search;
using Equine.Api.Features.Audit;
using Equine.Api.Features.Export;
using Equine.Api.Features.Tenants;
using Equine.Api.Tenancy;
using Equine.Infrastructure.Tenancy;
using Equine.Api.Health;
using Equine.Infrastructure.Storage;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Equine.Api.Cors;
using Equine.Infrastructure.Notifications;
using Equine.Infrastructure.Practice;
using Equine.Infrastructure.Sms;
using Hangfire;
using Hangfire.PostgreSql;
using Equine.Domain.JournalTemplates;
using Equine.Domain.Locations;
using Equine.Domain.Scheduling;
using Equine.Infrastructure.Scheduling;
using Equine.Infrastructure;
using Equine.Infrastructure.Email;
using Equine.Infrastructure.Tokens;
using Equine.Infrastructure.Widget;
using Equine.Jobs;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using FluentValidation;
using FluentValidation.AspNetCore;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Equine.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ──
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Equine.Api")
    .WriteTo.Console()
    .WriteTo.File("logs/equine-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();

builder.Host.UseSerilog();

// ── Services ──
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "HästJournal API",
        Version = "v1",
        Description = "Booking- och journalsystem f&#246;r djurbehandling"
    });
});

builder.Services.AddInfrastructure(builder.Configuration);
if (builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddSingleton<IObjectStorage, MemoryObjectStorage>();
builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ISlotCache, MemorySlotCache>();
builder.Services.AddSingleton<ITravelBuffer, FlatPerZoneTravelBuffer>();
builder.Services.AddScoped<SlotQueryService>();
builder.Services.AddScoped<LocationResolver>();
builder.Services.AddScoped<BookingCommandService>();
builder.Services.AddScoped<TravelRouteService>();
if (builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddSingleton<IPostcodeGeocoder, KnownPostcodeGeocoder>();
else
    builder.Services.AddScoped<IPostcodeGeocoder, NominatimPostcodeGeocoder>();
builder.Services.AddScoped<SchemaPlaceResolver>();
builder.Services.AddScoped<SchemaDayService>();
builder.Services.AddDataProtection();
builder.Services.AddScoped<PublicBookingTokenService>();
builder.Services.AddScoped<WidgetSettingsService>();
builder.Services.AddScoped<PractitionerResolver>();
builder.Services.AddScoped<PublicBookingService>();
builder.Services.AddScoped<PublicBookingRateGuard>();
builder.Services.AddScoped<IBookingMailer, BookingMailer>();
builder.Services.AddScoped<ExpireUnverifiedBookingsService>();
builder.Services.AddScoped<PracticeSettingsService>();
builder.Services.AddScoped<NotificationSettingsService>();
builder.Services.AddScoped<INotificationScheduler, NotificationScheduler>();
builder.Services.AddScoped<NotificationDispatcher>();
builder.Services.AddScoped<BookingNotifier>();
builder.Services.AddScoped<NotificationJobs>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<DriveLogService>();
builder.Services.AddScoped<Equine.Infrastructure.Tokens.ConsumedMagicLinkStore>();
builder.Services.AddSingleton<DnsDeliverabilityChecker>();
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<PostmarkOptions>(builder.Configuration.GetSection("Postmark"));
builder.Services.Configure<ElksOptions>(builder.Configuration.GetSection("Elks"));
builder.Services.Configure<Equine.Api.Maps.GoogleMapsOptions>(builder.Configuration.GetSection("GoogleMaps"));

var isProduction = builder.Environment.IsProduction();
var isDevelopment = builder.Environment.IsDevelopment();
if (isProduction)
{
    builder.Services.AddHttpClient<IEmailSender, PostmarkEmailSender>();
    builder.Services.AddHttpClient<ISmsSender, ElksSmsSender>();
}
else if (isDevelopment)
{
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
    builder.Services.AddSingleton<RecordingSmsSender>();
    builder.Services.AddSingleton<ISmsSender>(sp => sp.GetRequiredService<RecordingSmsSender>());
}
else
{
    builder.Services.AddSingleton<RecordingEmailSender>();
    builder.Services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<RecordingEmailSender>());
    builder.Services.AddSingleton<RecordingSmsSender>();
    builder.Services.AddSingleton<ISmsSender>(sp => sp.GetRequiredService<RecordingSmsSender>());
}
builder.Services.AddSingleton<IWebPushSender, RecordingWebPushSender>();

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHangfire(cfg => cfg
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(options =>
            options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("Default")!)));
    builder.Services.AddHangfireServer();
}
builder.Services.AddHttpClient("Nominatim", client =>
{
    client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("HastJournal/1.0 (schema travel)");
    client.Timeout = TimeSpan.FromSeconds(8);
});
builder.Services.AddHttpClient("Osrm", client =>
{
    client.BaseAddress = new Uri("https://router.project-osrm.org/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("HastJournal/1.0 (schema travel)");
    client.Timeout = TimeSpan.FromSeconds(8);
});
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddNpgSql(
        builder.Configuration.GetConnectionString("Default")!,
        name: "postgresql",
        failureStatus: null,
        tags: ["ready"])
    .AddCheck<ObjectStorageHealthCheck>("object-storage", tags: ["ready"]);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var readLimit = builder.Configuration.GetValue("RateLimiting:PublicReadPermitLimit", 50);
    var readWindow = builder.Configuration.GetValue("RateLimiting:PublicReadWindowSeconds", 15);
    var bookLimit = builder.Configuration.GetValue("RateLimiting:PublicBookingPermitLimit", 5);
    var bookWindow = builder.Configuration.GetValue("RateLimiting:PublicBookingWindowMinutes", 60);

    options.AddPolicy("public-read", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = readLimit,
                Window = TimeSpan.FromSeconds(readWindow),
                QueueLimit = 0
            }));

    options.AddPolicy("public-book", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            "book:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = bookLimit,
                Window = TimeSpan.FromMinutes(bookWindow),
                QueueLimit = 0
            }));
});

builder.Services.AddCors();
builder.Services.AddSingleton<ICorsPolicyProvider, DynamicCorsPolicyProvider>();

var app = builder.Build();

// ── Seeding ──
await EnsureDatabaseExists(app);
await SeedAdminUser(app);

// ── Middleware pipeline ──
app.UseSerilogRequestLogging();

app.UseCorrelationId();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "HästJournal API v1"));
}

app.UseExceptionHandler();
if (app.Environment.IsProduction())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseMiddleware<Equine.Api.Middleware.SecurityHeadersMiddleware>();
app.UseStaticFiles();
app.UseRateLimiter();
app.UseCors("App");
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<Equine.Api.Middleware.WidgetCspMiddleware>();
app.UseAuthorization();

// ── Health checks ──
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready")
});

// ── API Routes ──
var publicGroup = app.MapGroup("/api/public");
var appGroup = app.MapGroup("/api/app");

publicGroup.MapGet("/ping", () => Results.Json(new { ping = "pong" }))
    .WithName("PublicPing")
    .RequireCors("Public")
    .RequireRateLimiting("public-read");

app.MapTenantRegistrationEndpoints();
app.MapPublicBookingEndpoints();
app.MapWidgetSettingsEndpoints();
app.MapPracticeSettingsEndpoints();
app.MapAnatomyImageEndpoints();
app.MapPlaceSearchEndpoints();
app.MapNotificationSettingsEndpoints();
app.MapInAppNotificationEndpoints();
app.MapFollowUpEndpoints();
app.MapMarketingEndpoints();
app.MapPortalEndpoints();
app.MapWebhookEndpoints();

// Auth endpoints (partially anonymous)
app.MapAuthEndpoints();

// Feature endpoints
app.MapOwnerEndpoints();
app.MapHorseEndpoints();
app.MapTreatmentTypeEndpoints();
app.MapJournalEndpoints();
app.MapAttachmentEndpoints();
app.MapBookingEndpoints();
app.MapAvailabilityEndpoints();
app.MapSchemaEndpoints();
app.MapReportEndpoints();
app.MapSearchEndpoints();
app.MapAuditEndpoints();
app.MapExportEndpoints();

// App endpoints (authenticated)
appGroup.MapGet("/me", async (HttpContext context, UserManager<ApplicationUser> userManager) =>
{
    var authHeader = context.Request.Headers["Authorization"].ToString();
    Log.Information("/api/app/me called - Auth header present: {HasAuth}", !string.IsNullOrEmpty(authHeader));

    if (!context.User.Identity?.IsAuthenticated == true)
    {
        Log.Warning("/api/app/me: User not authenticated");
        return Results.Unauthorized();
    }

    var user = await userManager.FindByIdAsync(context.User.FindFirstValue(ClaimTypes.NameIdentifier));
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var db = context.RequestServices.GetRequiredService<EquineDbContext>();
    var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == user.TenantId);

    return Results.Json(new
    {
        id = user.Id.ToString(),
        email = user.Email,
        displayName = user.DisplayName,
        roles = (await userManager.GetRolesAsync(user)).ToArray(),
        twoFactorEnabled = user.TwoFactorEnabled,
        isFirstLogin = user.IsFirstLogin,
        tenant = tenant is null ? null : new
        {
            id = tenant.Id,
            name = tenant.Name,
            slug = tenant.Slug,
            plan = tenant.Plan.ToString()
        }
    });
}).RequireAuthorization();

appGroup.MapPatch("/me", async (
    HttpContext context,
    UserManager<ApplicationUser> userManager,
    Equine.Infrastructure.Audit.IAuditWriter audit,
    Equine.Api.Features.Settings.AccountUpdateRequest request,
    CancellationToken ct) =>
{
    var user = await userManager.FindByIdAsync(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    if (user is null) return Results.Unauthorized();
    if (!string.IsNullOrWhiteSpace(request.DisplayName))
        user.DisplayName = request.DisplayName.Trim();
    if (!string.IsNullOrWhiteSpace(request.Email))
    {
        user.Email = request.Email.Trim();
        user.UserName = user.Email;
        user.NormalizedEmail = user.Email.ToUpperInvariant();
        user.NormalizedUserName = user.NormalizedEmail;
    }
    user.UpdatedAt = DateTimeOffset.UtcNow;
    await userManager.UpdateAsync(user);
    await audit.WriteAsync(user.Id.ToString(), "ACCOUNT_UPDATE", "ApplicationUser", user.Id.ToString(), null, null, ct);
    return Results.Ok(new { user.Id, user.Email, user.DisplayName, user.TwoFactorEnabled });
}).RequireAuthorization();

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireAdminFilter()]
    });
    RecurringJob.AddOrUpdate<NotificationJobs>("dispatch-due", j => j.DispatchDue(), "*/1 * * * *");
    RecurringJob.AddOrUpdate<NotificationJobs>("expire-unverified", j => j.ExpireUnverified(), "*/1 * * * *");
    RecurringJob.AddOrUpdate<NotificationJobs>("follow-ups", j => j.ScanFollowUps(), "0 7 * * *");
    RecurringJob.AddOrUpdate<NotificationJobs>("unsigned-journals", j => j.ScanUnsignedJournals(), "0 * * * *");
    RecurringJob.AddOrUpdate<NotificationJobs>("daily-summary", j => j.SendDailySummary(), "*/15 * * * *");
    RecurringJob.AddOrUpdate<NotificationJobs>("weekly-digest", j => j.SendWeeklyDigest(), "0 7 * * 0");
    RecurringJob.AddOrUpdate<NotificationJobs>("purge-logs", j => j.PurgeLogs(), "0 3 * * 0");
    RecurringJob.AddOrUpdate<NotificationJobs>("deliverability", j => j.CheckDeliverability(), "0 8 * * *");
}

app.MapGet("/", () => Results.Redirect("/admin/"));
app.MapFallback(async (HttpContext context) =>
{
    var path = context.Request.Path.Value ?? "";
    string? folder = path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase) ? "admin"
        : path.StartsWith("/widget", StringComparison.OrdinalIgnoreCase) ? "widget"
        : path.StartsWith("/portal", StringComparison.OrdinalIgnoreCase) ? "portal"
        : null;

    if (folder is null || Path.HasExtension(path))
        return Results.NotFound();

    var index = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", folder, "index.html");
    return !File.Exists(index) ? Results.NotFound() : Results.File(index, "text/html");
});

app.Run();

async Task EnsureDatabaseExists(WebApplication app)
{
    using var scope = app.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<EquineDbContext>();

    if (!app.Environment.IsProduction())
    {
        var creator = context.Database.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>();
        if (!await creator.ExistsAsync())
            await creator.CreateAsync();

        await DatabaseSchema.MigrateAsync(context);
    }

    await EnsureDefaultTenant(scope.ServiceProvider);
    await BackfillTreatmentSlugs(context);
    await BackfillAnatomyFindingOptions(context);
}

async Task EnsureDefaultTenant(IServiceProvider services)
{
    var context = services.GetRequiredService<EquineDbContext>();
    var tenantContext = services.GetRequiredService<ITenantContext>();
    var provision = services.GetRequiredService<TenantProvisioningService>();
    var configuration = services.GetRequiredService<IConfiguration>();

    var tenant = await context.Tenants.OrderBy(t => t.CreatedAt).FirstOrDefaultAsync();
    if (tenant is null)
    {
        var name = configuration["Practitioner:Clinic"] ?? "HästJournal";
        tenant = new Equine.Domain.Entities.Tenant(name, "default");
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
    }

    tenantContext.SetTenant(tenant.Id, tenant.Slug);
    await provision.SeedDefaultsAsync();
}

async Task BackfillTreatmentSlugs(EquineDbContext context)
{
    var types = await context.TreatmentTypes.ToListAsync();
    foreach (var type in types)
    {
        if (string.IsNullOrWhiteSpace(type.Slug))
            type.Update(slug: type.Name);
    }
    if (context.ChangeTracker.HasChanges())
        await context.SaveChangesAsync();
}

async Task BackfillAnatomyFindingOptions(EquineDbContext context)
{
    var types = await context.TreatmentTypes
        .Where(t => t.JournalTemplateJson != null)
        .ToListAsync();
    foreach (var type in types)
    {
        var migrated = AnatomyFindingMigrator.TryMigrateTemplateJson(type.JournalTemplateJson);
        if (migrated is null)
            continue;
        type.Update(
            publicDescription: type.PublicDescription,
            colour: type.Colour,
            allowedLocationTypes: type.AllowedLocationTypes,
            followUpIntervalDays: type.FollowUpIntervalDays,
            journalTemplateJson: migrated);
    }

    var legacyPattern = $"%{FindingOptionsDefaults.LegacyFindingLabel}%";
    var journals = await context.JournalEntries
        .FromSqlRaw(
            """SELECT * FROM "journal_entries" WHERE "TemplateDataJson"::text LIKE {0}""",
            legacyPattern)
        .ToListAsync();
    foreach (var journal in journals)
    {
        var migrated = AnatomyFindingMigrator.TryMigrateJournalDataJson(journal.TemplateDataJson);
        if (migrated is not null)
            journal.TryApplyTemplateDataMigration(migrated);
    }

    if (context.ChangeTracker.HasChanges())
        await context.SaveChangesAsync();
}

// ── Admin User Seeder ──
async Task SeedAdminUser(WebApplication app)
{
    using var scope = app.Services.CreateAsyncScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    var context = scope.ServiceProvider.GetRequiredService<EquineDbContext>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    foreach (var role in new[] { "Admin", "Practitioner", "Assistant", "Client" })
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }
    }

    var adminEmail = configuration["Admin:Email"] ?? "admin@equijournal.se";
    var adminPassword = configuration["Admin:Password"] ?? "Admin@123456";
    var adminDisplayName = configuration["Admin:DisplayName"] ?? "Admin";

    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser is not null)
        return;

    var tenant = await context.Tenants.OrderBy(t => t.CreatedAt).FirstAsync();

    adminUser = new ApplicationUser
    {
        Id = Guid.CreateVersion7(),
        TenantId = tenant.Id,
        Email = adminEmail,
        UserName = adminEmail,
        DisplayName = adminDisplayName,
        EmailConfirmed = true,
        IsFirstLogin = false,
        TwoFactorEnabled = false,
        CreatedAt = DateTimeOffset.UtcNow,
        NormalizedEmail = adminEmail.ToUpperInvariant(),
        NormalizedUserName = adminEmail.ToUpperInvariant()
    };

    foreach (var validator in userManager.PasswordValidators)
    {
        var passwordCheck = await validator.ValidateAsync(userManager, adminUser, adminPassword);
        if (passwordCheck.Succeeded)
            continue;

        var policyErrors = string.Join(", ", passwordCheck.Errors.Select(e => e.Description));
        Log.Fatal(
            "Admin user was not created for {Email}. Admin__Password does not meet policy: {Errors}. " +
            "Use at least 12 characters with upper, lower, digit, and symbol.",
            adminEmail,
            policyErrors);
        if (app.Environment.IsDevelopment())
            throw new InvalidOperationException($"Admin seed failed for {adminEmail}: {policyErrors}");
        return;
    }

    var result = await userManager.CreateAsync(adminUser, adminPassword);
    if (!result.Succeeded)
    {
        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
        Log.Fatal("Failed to create admin user {Email}: {Errors}", adminEmail, errors);
        if (app.Environment.IsDevelopment())
            throw new InvalidOperationException($"Admin seed failed for {adminEmail}: {errors}");
        return;
    }
    await userManager.AddToRoleAsync(adminUser, "Admin");
    Log.Information("Admin user seeded: {Email} (id: {Id})", adminEmail, adminUser.Id);
}

public partial class Program { }
