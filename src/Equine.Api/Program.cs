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
app.UseCors(DynamicCorsPolicyProvider.AppPolicy);
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
}).RequireAuthorization("CanManageBookings");

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
}).RequireAuthorization("CanManageBookings");

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
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    if (!app.Environment.IsProduction())
    {
        var creator = context.Database.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>();
        if (!await creator.ExistsAsync())
            await creator.CreateAsync();

        await EnsureExtensionAsync(context, "citext");
        await EnsureExtensionAsync(context, "btree_gist");
        await EnsureExtensionAsync(context, "pg_trgm");

        await context.Database.EnsureCreatedAsync();
        await EnableExtensions(context);
    }

    await TenancySchemaUpgrade.ApplyAsync(context);
    await EnsureZoneGeometryColumns(context);
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

async Task EnsureZoneGeometryColumns(EquineDbContext context)
{
    await context.Database.ExecuteSqlRawAsync(@"
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'zones'
            ) THEN
                ALTER TABLE ""zones"" ADD COLUMN IF NOT EXISTS ""GeometryKind"" varchar(20) NULL;
                ALTER TABLE ""zones"" ADD COLUMN IF NOT EXISTS ""GeometryJson"" jsonb NULL;
                ALTER TABLE ""zones"" ADD COLUMN IF NOT EXISTS ""CenterLatitude"" double precision NULL;
                ALTER TABLE ""zones"" ADD COLUMN IF NOT EXISTS ""CenterLongitude"" double precision NULL;
                ALTER TABLE ""zones"" ADD COLUMN IF NOT EXISTS ""RadiusKm"" double precision NULL;
                ALTER TABLE ""zones"" ADD COLUMN IF NOT EXISTS ""BufferKm"" double precision NOT NULL DEFAULT 0;
                ALTER TABLE ""zones"" ADD COLUMN IF NOT EXISTS ""EffectiveGeometryJson"" jsonb NULL;
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'zones' AND column_name = 'PostcodesJson'
                ) THEN
                    ALTER TABLE ""zones"" ALTER COLUMN ""PostcodesJson"" SET DEFAULT '[]';
                    UPDATE ""zones"" SET ""PostcodesJson"" = '[]' WHERE ""PostcodesJson"" IS NULL;
                END IF;
            END IF;
        END $$;
    ");
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

async Task EnsureExtensionAsync(EquineDbContext context, string name)
{
    try
    {
        await context.Database.ExecuteSqlRawAsync($"CREATE EXTENSION IF NOT EXISTS {name};");
    }
    catch (Npgsql.PostgresException ex) when (ex.SqlState is "23505" or "42710")
    {
        // Parallel startups can race IF NOT EXISTS.
    }
}

async Task EnableExtensions(EquineDbContext context)
{
    await EnsureExtensionAsync(context, "citext");
    await EnsureExtensionAsync(context, "btree_gist");
    await EnsureExtensionAsync(context, "pg_trgm");

    await context.Database.ExecuteSqlRawAsync(@"
        ALTER TABLE ""journal_entries"" ADD COLUMN IF NOT EXISTS ""UpdatedAt"" timestamptz DEFAULT now();
        ALTER TABLE ""journal_entries"" ADD COLUMN IF NOT EXISTS ""CreatedAt"" timestamptz DEFAULT now();
        ALTER TABLE ""journal_entries"" ADD COLUMN IF NOT EXISTS ""SignedAt"" timestamptz NULL;
        ALTER TABLE ""journal_entries"" ADD COLUMN IF NOT EXISTS ""SignedBy"" varchar(255) NULL;
        ALTER TABLE ""journal_entries"" ADD COLUMN IF NOT EXISTS ""ContentHash"" varchar(64) NULL;
        ALTER TABLE ""journal_entries"" ADD COLUMN IF NOT EXISTS ""Status"" varchar(20) DEFAULT 'Draft';
        ALTER TABLE ""journal_entries"" ADD COLUMN IF NOT EXISTS ""Source"" varchar(20) DEFAULT 'Practice';
    ");

    // Swedish full-text search: tsvector computed column + GIN index
    // Note: column names must be double-quoted because EF Core creates them as PascalCase (quoted)
    await context.Database.ExecuteSqlRawAsync(@"
        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_name = 'journal_entries' AND column_name = 'search_vector'
            ) THEN
                ALTER TABLE ""journal_entries""
                ADD COLUMN ""search_vector"" tsvector
                GENERATED ALWAYS AS (
                    to_tsvector('swedish',
                        coalesce(""Anamnes"", '') || ' ' ||
                        coalesce(""StatusKlinisk"", '') || ' ' ||
                        coalesce(""Atgarder"", '') || ' ' ||
                        coalesce(""Diagnos"", '') || ' ' ||
                        coalesce(""PrognosOchPlan"", '')
                    )
                ) STORED;
            END IF;
        END $$;
    ");

    await context.Database.ExecuteSqlRawAsync(@"
        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1 FROM pg_indexes
                WHERE tablename = 'journal_entries' AND indexname = 'idx_journal_entries_search_vector_gin'
            ) THEN
                CREATE INDEX ""idx_journal_entries_search_vector_gin""
                ON ""journal_entries"" USING GIN (""search_vector"");
            END IF;
        END $$;
    ");

    await context.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS ""locations"" (
            ""Id"" uuid PRIMARY KEY,
            ""Type"" varchar(30) NOT NULL,
            ""Name"" varchar(200) NOT NULL,
            ""AddressStreet"" varchar(200) NULL,
            ""AddressPostcode"" varchar(10) NULL,
            ""AddressCity"" varchar(100) NULL,
            ""Latitude"" numeric NULL,
            ""Longitude"" numeric NULL,
            ""NormalizedKey"" varchar(400) NOT NULL,
            ""CreatedAt"" timestamptz NOT NULL DEFAULT now(),
            ""UpdatedAt"" timestamptz NOT NULL DEFAULT now()
        );
        CREATE TABLE IF NOT EXISTS ""zones"" (
            ""Id"" uuid PRIMARY KEY,
            ""Name"" varchar(200) NOT NULL UNIQUE,
            ""PostcodesJson"" jsonb NOT NULL DEFAULT '[]',
            ""GeometryKind"" varchar(20) NULL,
            ""GeometryJson"" jsonb NULL,
            ""CenterLatitude"" double precision NULL,
            ""CenterLongitude"" double precision NULL,
            ""RadiusKm"" double precision NULL,
            ""BufferKm"" double precision NOT NULL DEFAULT 0,
            ""EffectiveGeometryJson"" jsonb NULL,
            ""TravelBufferMinutes"" integer NOT NULL,
            ""IsFallback"" boolean NOT NULL,
            ""CreatedAt"" timestamptz NOT NULL DEFAULT now(),
            ""UpdatedAt"" timestamptz NOT NULL DEFAULT now()
        );
        ALTER TABLE ""zones"" ADD COLUMN IF NOT EXISTS ""GeometryKind"" varchar(20) NULL;
        ALTER TABLE ""zones"" ADD COLUMN IF NOT EXISTS ""GeometryJson"" jsonb NULL;
        ALTER TABLE ""zones"" ADD COLUMN IF NOT EXISTS ""CenterLatitude"" double precision NULL;
        ALTER TABLE ""zones"" ADD COLUMN IF NOT EXISTS ""CenterLongitude"" double precision NULL;
        ALTER TABLE ""zones"" ADD COLUMN IF NOT EXISTS ""RadiusKm"" double precision NULL;
        ALTER TABLE ""zones"" ADD COLUMN IF NOT EXISTS ""BufferKm"" double precision NOT NULL DEFAULT 0;
        ALTER TABLE ""zones"" ADD COLUMN IF NOT EXISTS ""EffectiveGeometryJson"" jsonb NULL;
        CREATE TABLE IF NOT EXISTS ""availability_rules"" (
            ""Id"" uuid PRIMARY KEY,
            ""PractitionerId"" uuid NOT NULL,
            ""DayOfWeek"" integer NOT NULL,
            ""StartTime"" time NOT NULL,
            ""EndTime"" time NOT NULL,
            ""EffectiveFrom"" date NULL,
            ""EffectiveTo"" date NULL,
            ""ZoneId"" uuid NULL REFERENCES ""zones"" (""Id"") ON DELETE SET NULL,
            ""Active"" boolean NOT NULL,
            ""CreatedAt"" timestamptz NOT NULL DEFAULT now(),
            ""UpdatedAt"" timestamptz NOT NULL DEFAULT now()
        );
        CREATE TABLE IF NOT EXISTS ""time_off"" (
            ""Id"" uuid PRIMARY KEY,
            ""PractitionerId"" uuid NOT NULL,
            ""StartsAt"" timestamptz NOT NULL,
            ""EndsAt"" timestamptz NOT NULL,
            ""AllDay"" boolean NOT NULL,
            ""Reason"" varchar(500) NULL,
            ""RecurringAnnual"" boolean NOT NULL,
            ""CreatedAt"" timestamptz NOT NULL DEFAULT now(),
            ""UpdatedAt"" timestamptz NOT NULL DEFAULT now()
        );
        CREATE TABLE IF NOT EXISTS ""visits"" (
            ""Id"" uuid PRIMARY KEY,
            ""PractitionerId"" uuid NOT NULL,
            ""LocationId"" uuid NOT NULL REFERENCES ""locations"" (""Id"") ON DELETE RESTRICT,
            ""StartsAt"" timestamptz NOT NULL,
            ""EndsAt"" timestamptz NOT NULL,
            ""OccupiesSlot"" boolean NOT NULL,
            ""Source"" varchar(20) NOT NULL,
            ""CreatedAt"" timestamptz NOT NULL DEFAULT now(),
            ""UpdatedAt"" timestamptz NOT NULL DEFAULT now()
        );
        CREATE TABLE IF NOT EXISTS ""booking_lines"" (
            ""Id"" uuid PRIMARY KEY,
            ""VisitId"" uuid NOT NULL REFERENCES ""visits"" (""Id"") ON DELETE CASCADE,
            ""HorseId"" uuid NOT NULL,
            ""OwnerId"" uuid NOT NULL,
            ""TreatmentTypeId"" uuid NOT NULL,
            ""TreatmentName"" varchar(200) NOT NULL,
            ""DurationMinutes"" integer NOT NULL,
            ""Price"" numeric(10,2) NOT NULL,
            ""VatRate"" numeric(5,4) NOT NULL,
            ""Status"" varchar(20) NOT NULL,
            ""Source"" varchar(20) NOT NULL,
            ""SortOrder"" integer NOT NULL,
            ""ClientNote"" varchar(2000) NULL,
            ""InternalNote"" varchar(2000) NULL,
            ""CancellationReason"" varchar(500) NULL,
            ""CancelledAt"" timestamptz NULL,
            ""CancelledBy"" uuid NULL,
            ""JournalEntryId"" uuid NULL,
            ""CreatedAt"" timestamptz NOT NULL DEFAULT now(),
            ""UpdatedAt"" timestamptz NOT NULL DEFAULT now()
        );
        ALTER TABLE ""journal_entries"" ADD COLUMN IF NOT EXISTS ""BookingLineId"" uuid NULL;
        ALTER TABLE ""owners"" ADD COLUMN IF NOT EXISTS ""EmailVerified"" boolean NOT NULL DEFAULT true;
        ALTER TABLE ""horses"" ADD COLUMN IF NOT EXISTS ""EmailVerified"" boolean NOT NULL DEFAULT true;
        ALTER TABLE ""treatment_types"" ADD COLUMN IF NOT EXISTS ""Slug"" varchar(80) NOT NULL DEFAULT '';
        ALTER TABLE ""booking_lines"" ADD COLUMN IF NOT EXISTS ""EmailVerifiedAt"" timestamptz NULL;
        ALTER TABLE ""booking_lines"" ADD COLUMN IF NOT EXISTS ""PublicReference"" varchar(20) NULL;
        CREATE TABLE IF NOT EXISTS ""widget_settings"" (
            ""Id"" uuid PRIMARY KEY,
            ""AllowedOriginsJson"" jsonb NOT NULL,
            ""ShowPrices"" boolean NOT NULL,
            ""BookingTerms"" varchar(8000) NULL,
            ""PrivacyPolicyUrl"" varchar(500) NULL,
            ""VerificationWindowMinutes"" integer NOT NULL,
            ""CancellationNoticeHours"" integer NOT NULL,
            ""PublicBaseUrl"" varchar(300) NOT NULL,
            ""ContactPhone"" varchar(50) NULL,
            ""ContactEmail"" varchar(255) NULL,
            ""UpdatedAt"" timestamptz NOT NULL DEFAULT now()
        );
        ALTER TABLE ""owners"" ADD COLUMN IF NOT EXISTS ""EmailInvalid"" boolean NOT NULL DEFAULT false;
        ALTER TABLE ""owners"" ADD COLUMN IF NOT EXISTS ""UserId"" uuid NULL;
        ALTER TABLE ""horses"" ADD COLUMN IF NOT EXISTS ""FollowUpOverrideDays"" integer NULL;
        ALTER TABLE ""horses"" ADD COLUMN IF NOT EXISTS ""FollowUpSnoozedUntil"" timestamptz NULL;
        ALTER TABLE ""horses"" ADD COLUMN IF NOT EXISTS ""FollowUpDismissedAt"" timestamptz NULL;
        ALTER TABLE ""horses"" ADD COLUMN IF NOT EXISTS ""FollowUpDismissedReason"" varchar(500) NULL;
        ALTER TABLE ""treatment_types"" ADD COLUMN IF NOT EXISTS ""ShareSummaryWithClient"" boolean NOT NULL DEFAULT false;
        CREATE TABLE IF NOT EXISTS ""practice_settings"" (
            ""Id"" uuid PRIMARY KEY,
            ""Name"" varchar(200) NOT NULL,
            ""Clinic"" varchar(200) NOT NULL,
            ""Address"" varchar(400) NULL,
            ""AddressStreet"" varchar(200) NULL,
            ""AddressPostcode"" varchar(10) NULL,
            ""AddressCity"" varchar(100) NULL,
            ""Latitude"" numeric NULL,
            ""Longitude"" numeric NULL,
            ""Phone"" varchar(50) NULL,
            ""Email"" varchar(255) NULL,
            ""VehicleRegistrationNumber"" varchar(16) NOT NULL DEFAULT '',
            ""UpdatedAt"" timestamptz NOT NULL DEFAULT now()
        );
        ALTER TABLE ""practice_settings"" ADD COLUMN IF NOT EXISTS ""AddressStreet"" varchar(200) NULL;
        ALTER TABLE ""practice_settings"" ADD COLUMN IF NOT EXISTS ""AddressPostcode"" varchar(10) NULL;
        ALTER TABLE ""practice_settings"" ADD COLUMN IF NOT EXISTS ""AddressCity"" varchar(100) NULL;
        ALTER TABLE ""practice_settings"" ADD COLUMN IF NOT EXISTS ""VehicleRegistrationNumber"" varchar(16) NULL;
        UPDATE ""practice_settings"" SET ""AddressStreet"" = COALESCE(""AddressStreet"", '');
        UPDATE ""practice_settings"" SET ""AddressPostcode"" = COALESCE(""AddressPostcode"", '');
        UPDATE ""practice_settings"" SET ""AddressCity"" = COALESCE(""AddressCity"", '');
        UPDATE ""practice_settings"" SET ""VehicleRegistrationNumber"" = COALESCE(""VehicleRegistrationNumber"", '');
        ALTER TABLE ""practice_settings"" ALTER COLUMN ""VehicleRegistrationNumber"" SET DEFAULT '';
        CREATE TABLE IF NOT EXISTS ""notification_settings"" (
            ""Id"" uuid PRIMARY KEY,
            ""EnabledJson"" jsonb NOT NULL,
            ""ReminderLeadHours"" integer NOT NULL,
            ""QuietHoursStart"" time NOT NULL,
            ""QuietHoursEnd"" time NOT NULL,
            ""DailySmsCap"" integer NOT NULL,
            ""MorningSummaryTime"" time NOT NULL,
            ""UpdatedAt"" timestamptz NOT NULL DEFAULT now()
        );
        CREATE TABLE IF NOT EXISTS ""notification_templates"" (
            ""Id"" uuid PRIMARY KEY,
            ""Type"" varchar(60) NOT NULL,
            ""Channel"" varchar(20) NOT NULL,
            ""Subject"" varchar(300) NULL,
            ""Body"" varchar(16000) NOT NULL,
            ""UpdatedAt"" timestamptz NOT NULL DEFAULT now()
        );
        CREATE TABLE IF NOT EXISTS ""scheduled_notifications"" (
            ""Id"" uuid PRIMARY KEY,
            ""Type"" varchar(60) NOT NULL,
            ""Channel"" varchar(20) NOT NULL,
            ""Recipient"" varchar(255) NOT NULL,
            ""RelatedEntityType"" varchar(80) NOT NULL,
            ""RelatedEntityId"" uuid NOT NULL,
            ""ScheduledFor"" timestamptz NOT NULL,
            ""PayloadJson"" jsonb NOT NULL,
            ""Status"" varchar(20) NOT NULL,
            ""Attempts"" integer NOT NULL,
            ""LastError"" varchar(2000) NULL,
            ""SentAt"" timestamptz NULL,
            ""ProviderMessageId"" varchar(200) NULL,
            ""CreatedAt"" timestamptz NOT NULL DEFAULT now()
        );
        CREATE TABLE IF NOT EXISTS ""notification_log"" (
            ""Id"" uuid PRIMARY KEY,
            ""Type"" varchar(60) NOT NULL,
            ""Channel"" varchar(20) NOT NULL,
            ""Recipient"" varchar(255) NULL,
            ""TemplateId"" uuid NULL,
            ""Subject"" varchar(300) NULL,
            ""Body"" varchar(16000) NULL,
            ""RelatedEntityType"" varchar(80) NULL,
            ""RelatedEntityId"" uuid NOT NULL,
            ""Status"" varchar(40) NOT NULL,
            ""ProviderMessageId"" varchar(200) NULL,
            ""ProviderResponse"" varchar(4000) NULL,
            ""CreatedAt"" timestamptz NOT NULL DEFAULT now()
        );
        CREATE TABLE IF NOT EXISTS ""in_app_notifications"" (
            ""Id"" uuid PRIMARY KEY,
            ""UserId"" uuid NOT NULL,
            ""Type"" varchar(60) NOT NULL,
            ""Title"" varchar(200) NOT NULL,
            ""Body"" varchar(2000) NOT NULL,
            ""Link"" varchar(400) NULL,
            ""CreatedAt"" timestamptz NOT NULL DEFAULT now(),
            ""ReadAt"" timestamptz NULL
        );
        CREATE TABLE IF NOT EXISTS ""web_push_endpoints"" (
            ""Id"" uuid PRIMARY KEY,
            ""UserId"" uuid NOT NULL,
            ""Endpoint"" varchar(1000) NOT NULL,
            ""P256dh"" varchar(255) NULL,
            ""Auth"" varchar(255) NULL,
            ""CreatedAt"" timestamptz NOT NULL DEFAULT now()
        );
        CREATE TABLE IF NOT EXISTS ""consumed_magic_link_tokens"" (
            ""TokenHash"" varchar(64) PRIMARY KEY,
            ""ConsumedAt"" timestamptz NOT NULL DEFAULT now()
        );
    ");

    await context.Database.ExecuteSqlRawAsync(@"
        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1 FROM pg_indexes WHERE indexname = 'idx_owners_name_trgm'
            ) THEN
                CREATE INDEX idx_owners_name_trgm ON owners USING gin (""Name"" gin_trgm_ops);
            END IF;
            IF NOT EXISTS (
                SELECT 1 FROM pg_indexes WHERE indexname = 'idx_horses_name_trgm'
            ) THEN
                CREATE INDEX idx_horses_name_trgm ON horses USING gin (""Name"" gin_trgm_ops);
            END IF;
        END $$;
    ");

    await context.Database.ExecuteSqlRawAsync(@"
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'visits'
            ) AND NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_name = 'visits' AND column_name = 'TimeRange'
            ) THEN
                ALTER TABLE ""visits""
                ADD COLUMN ""TimeRange"" tstzrange
                GENERATED ALWAYS AS (tstzrange(""StartsAt"", ""EndsAt"", '[)')) STORED;
            END IF;
        END $$;
    ");

    await context.Database.ExecuteSqlRawAsync(@"
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'visits'
            ) AND NOT EXISTS (
                SELECT 1 FROM pg_constraint WHERE conname = 'visits_no_overlap'
            ) THEN
                ALTER TABLE ""visits""
                ADD CONSTRAINT visits_no_overlap
                EXCLUDE USING gist (""PractitionerId"" WITH =, ""TimeRange"" WITH &&)
                WHERE (""OccupiesSlot"" = true);
            END IF;
        END $$;
    ");

    await context.Database.ExecuteSqlRawAsync(@"
        ALTER TABLE ""AspNetUsers"" ADD COLUMN IF NOT EXISTS ""RefreshTokenHash"" varchar(64) NULL;
        ALTER TABLE ""AspNetUsers"" ADD COLUMN IF NOT EXISTS ""RefreshTokenExpiresAt"" timestamptz NULL;
    ");
}

public partial class Program { }
