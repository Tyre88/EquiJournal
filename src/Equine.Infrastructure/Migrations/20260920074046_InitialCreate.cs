using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Equine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "citext", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Plan = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubscriptionStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StripeCustomerId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StripeSubscriptionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "public",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TotpSecret = table.Column<string>(type: "text", nullable: true),
                    IsFirstLogin = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    RefreshTokenHash = table.Column<string>(type: "text", nullable: true),
                    RefreshTokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUsers_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Before = table.Column<string>(type: "jsonb", nullable: true),
                    After = table.Column<string>(type: "jsonb", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_log_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "in_app_notifications",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Link = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_in_app_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_in_app_notifications_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "locations",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AddressStreet = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AddressPostcode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    AddressCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Latitude = table.Column<decimal>(type: "numeric", nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric", nullable: true),
                    NormalizedKey = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_locations_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_log",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Recipient = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Body = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    RelatedEntityType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProviderResponse = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_log", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notification_log_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_settings",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnabledJson = table.Column<string>(type: "jsonb", nullable: false),
                    ReminderLeadHours = table.Column<int>(type: "integer", nullable: false),
                    QuietHoursStart = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    QuietHoursEnd = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    DailySmsCap = table.Column<int>(type: "integer", nullable: false),
                    MorningSummaryTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notification_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_templates",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notification_templates_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "owners",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "citext", maxLength: 255, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AddressStreet = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AddressPostcode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    AddressCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Latitude = table.Column<decimal>(type: "numeric", nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MarketingConsent = table.Column<bool>(type: "boolean", nullable: false),
                    MarketingConsentAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    EmailVerified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    EmailInvalid = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_owners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_owners_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "practice_settings",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Clinic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    AddressStreet = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AddressPostcode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AddressCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric", nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric", nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    VehicleRegistrationNumber = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: ""),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_practice_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_practice_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "scheduled_notifications",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Recipient = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    RelatedEntityType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledFor = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheduled_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scheduled_notifications_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "time_off",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PractitionerId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    AllDay = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RecurringAnnual = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_time_off", x => x.Id);
                    table.ForeignKey(
                        name: "FK_time_off_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "treatment_types",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ShortDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PublicDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    BufferBeforeMinutes = table.Column<int>(type: "integer", nullable: false),
                    BufferAfterMinutes = table.Column<int>(type: "integer", nullable: false),
                    Colour = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PriceExclVat = table.Column<decimal>(type: "numeric", nullable: false),
                    VatRate = table.Column<decimal>(type: "numeric", nullable: false),
                    BookableOnline = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    MinNoticeHours = table.Column<int>(type: "integer", nullable: false),
                    MaxAdvanceDays = table.Column<int>(type: "integer", nullable: false),
                    AllowedLocationTypes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FollowUpIntervalDays = table.Column<int>(type: "integer", nullable: true),
                    ShareSummaryWithClient = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    JournalTemplateJson = table.Column<string>(type: "jsonb", nullable: true),
                    JournalTemplateVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_treatment_types", x => x.Id);
                    table.ForeignKey(
                        name: "FK_treatment_types_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "web_push_endpoints",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    P256dh = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Auth = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_web_push_endpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_web_push_endpoints_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "widget_settings",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowedOriginsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ShowPrices = table.Column<bool>(type: "boolean", nullable: false),
                    BookingTerms = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    PrivacyPolicyUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    VerificationWindowMinutes = table.Column<int>(type: "integer", nullable: false),
                    CancellationNoticeHours = table.Column<int>(type: "integer", nullable: false),
                    PublicBaseUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ContactEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_widget_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_widget_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "zones",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PostcodesJson = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    GeometryKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    GeometryJson = table.Column<string>(type: "jsonb", nullable: true),
                    CenterLatitude = table.Column<double>(type: "double precision", nullable: true),
                    CenterLongitude = table.Column<double>(type: "double precision", nullable: true),
                    RadiusKm = table.Column<double>(type: "double precision", nullable: true),
                    BufferKm = table.Column<double>(type: "double precision", nullable: false),
                    EffectiveGeometryJson = table.Column<string>(type: "jsonb", nullable: true),
                    TravelBufferMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsFallback = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_zones_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                schema: "public",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                schema: "public",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "public",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                schema: "public",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "visits",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PractitionerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    OccupiesSlot = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_visits_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "public",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_visits_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "horses",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Species = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Breed = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Sex = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BirthYear = table.Column<int>(type: "integer", nullable: true),
                    AgeGroup = table.Column<string>(type: "text", nullable: true),
                    Identity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Colour = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Markings = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StableLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StableAddress = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StablePostcode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    StableCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StableLatitude = table.Column<decimal>(type: "numeric", nullable: true),
                    StableLongitude = table.Column<decimal>(type: "numeric", nullable: true),
                    Background = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EmailVerified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Aktiv"),
                    FollowUpOverrideDays = table.Column<int>(type: "integer", nullable: true),
                    FollowUpSnoozedUntil = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    FollowUpDismissedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    FollowUpDismissedReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_horses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_horses_owners_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "public",
                        principalTable: "owners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_horses_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "availability_rules",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PractitionerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_availability_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_availability_rules_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_availability_rules_zones_ZoneId",
                        column: x => x.ZoneId,
                        principalSchema: "public",
                        principalTable: "zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "booking_lines",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    HorseId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    VatRate = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ClientNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InternalNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CancelledBy = table.Column<Guid>(type: "uuid", nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmailVerifiedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    PublicReference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_booking_lines_horses_HorseId",
                        column: x => x.HorseId,
                        principalSchema: "public",
                        principalTable: "horses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_booking_lines_owners_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "public",
                        principalTable: "owners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_booking_lines_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_booking_lines_treatment_types_TreatmentTypeId",
                        column: x => x.TreatmentTypeId,
                        principalSchema: "public",
                        principalTable: "treatment_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_booking_lines_visits_VisitId",
                        column: x => x.VisitId,
                        principalSchema: "public",
                        principalTable: "visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "journal_entries",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    HorseId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerSnapshot = table.Column<string>(type: "jsonb", nullable: false),
                    Species = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Sex = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AgeOrAgeGroup = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AnimalIdentity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PerformedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    Anamnes = table.Column<string>(type: "text", nullable: false),
                    StatusKlinisk = table.Column<string>(type: "text", nullable: false),
                    Atgarder = table.Column<string>(type: "text", nullable: false),
                    Diagnos = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Differentialdiagnoser = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PrognosOchPlan = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TreatmentTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    TreatmentTypeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    Price = table.Column<decimal>(type: "numeric", nullable: true),
                    TemplateDataJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    TemplateVersion = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    SignedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    SignedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    BookingLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Practice"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_journal_entries_horses_HorseId",
                        column: x => x.HorseId,
                        principalSchema: "public",
                        principalTable: "horses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entries_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attachments",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_attachments_journal_entries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalSchema: "public",
                        principalTable: "journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_attachments_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "journal_amendments",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_amendments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_journal_amendments_journal_entries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalSchema: "public",
                        principalTable: "journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_journal_amendments_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                schema: "public",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "public",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                schema: "public",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                schema: "public",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                schema: "public",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "public",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_TenantId",
                schema: "public",
                table: "AspNetUsers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "public",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attachments_JournalEntryId",
                schema: "public",
                table: "attachments",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_attachments_TenantId",
                schema: "public",
                table: "attachments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_TenantId",
                schema: "public",
                table: "audit_log",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_availability_rules_PractitionerId_DayOfWeek",
                schema: "public",
                table: "availability_rules",
                columns: new[] { "PractitionerId", "DayOfWeek" });

            migrationBuilder.CreateIndex(
                name: "IX_availability_rules_TenantId",
                schema: "public",
                table: "availability_rules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_availability_rules_ZoneId",
                schema: "public",
                table: "availability_rules",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_booking_lines_HorseId",
                schema: "public",
                table: "booking_lines",
                column: "HorseId");

            migrationBuilder.CreateIndex(
                name: "IX_booking_lines_OwnerId",
                schema: "public",
                table: "booking_lines",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_booking_lines_PublicReference",
                schema: "public",
                table: "booking_lines",
                column: "PublicReference",
                unique: true,
                filter: "\"PublicReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_booking_lines_Source_Status_EmailVerifiedAt",
                schema: "public",
                table: "booking_lines",
                columns: new[] { "Source", "Status", "EmailVerifiedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_lines_Status",
                schema: "public",
                table: "booking_lines",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_booking_lines_TenantId",
                schema: "public",
                table: "booking_lines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_booking_lines_TreatmentTypeId",
                schema: "public",
                table: "booking_lines",
                column: "TreatmentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_booking_lines_VisitId",
                schema: "public",
                table: "booking_lines",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "ix_horses_name_trgm",
                schema: "public",
                table: "horses",
                column: "Name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_horses_OwnerId",
                schema: "public",
                table: "horses",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_horses_TenantId",
                schema: "public",
                table: "horses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_in_app_notifications_TenantId",
                schema: "public",
                table: "in_app_notifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_in_app_notifications_UserId_ReadAt",
                schema: "public",
                table: "in_app_notifications",
                columns: new[] { "UserId", "ReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_journal_amendments_JournalEntryId",
                schema: "public",
                table: "journal_amendments",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_journal_amendments_TenantId",
                schema: "public",
                table: "journal_amendments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_BookingLineId",
                schema: "public",
                table: "journal_entries",
                column: "BookingLineId");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_HorseId",
                schema: "public",
                table: "journal_entries",
                column: "HorseId");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_HorseId_PerformedAt",
                schema: "public",
                table: "journal_entries",
                columns: new[] { "HorseId", "PerformedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_TenantId",
                schema: "public",
                table: "journal_entries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_locations_NormalizedKey",
                schema: "public",
                table: "locations",
                column: "NormalizedKey");

            migrationBuilder.CreateIndex(
                name: "IX_locations_TenantId",
                schema: "public",
                table: "locations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_log_CreatedAt",
                schema: "public",
                table: "notification_log",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_notification_log_ProviderMessageId",
                schema: "public",
                table: "notification_log",
                column: "ProviderMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_log_RelatedEntityId",
                schema: "public",
                table: "notification_log",
                column: "RelatedEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_log_TenantId",
                schema: "public",
                table: "notification_log",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_settings_TenantId",
                schema: "public",
                table: "notification_settings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_templates_TenantId",
                schema: "public",
                table: "notification_templates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_templates_TenantId_Type_Channel",
                schema: "public",
                table: "notification_templates",
                columns: new[] { "TenantId", "Type", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_owners_email_unique",
                schema: "public",
                table: "owners",
                columns: new[] { "TenantId", "Email" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_owners_name_trgm",
                schema: "public",
                table: "owners",
                column: "Name",
                filter: "\"DeletedAt\" IS NULL")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_owners_phone_trgm",
                schema: "public",
                table: "owners",
                column: "Phone",
                filter: "\"DeletedAt\" IS NULL")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_owners_TenantId",
                schema: "public",
                table: "owners",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_practice_settings_TenantId",
                schema: "public",
                table: "practice_settings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_notifications_pending_unique",
                schema: "public",
                table: "scheduled_notifications",
                columns: new[] { "TenantId", "Type", "RelatedEntityId", "Channel" },
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_notifications_Status_ScheduledFor",
                schema: "public",
                table: "scheduled_notifications",
                columns: new[] { "Status", "ScheduledFor" });

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_notifications_TenantId",
                schema: "public",
                table: "scheduled_notifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_slug",
                schema: "public",
                table: "tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_time_off_PractitionerId_StartsAt",
                schema: "public",
                table: "time_off",
                columns: new[] { "PractitionerId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_time_off_TenantId",
                schema: "public",
                table: "time_off",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_treatment_types_Name",
                schema: "public",
                table: "treatment_types",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_treatment_types_TenantId",
                schema: "public",
                table: "treatment_types",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_treatment_types_TenantId_Slug",
                schema: "public",
                table: "treatment_types",
                columns: new[] { "TenantId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_visits_LocationId",
                schema: "public",
                table: "visits",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_visits_OccupiesSlot",
                schema: "public",
                table: "visits",
                column: "OccupiesSlot");

            migrationBuilder.CreateIndex(
                name: "IX_visits_PractitionerId_StartsAt",
                schema: "public",
                table: "visits",
                columns: new[] { "PractitionerId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_visits_TenantId",
                schema: "public",
                table: "visits",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_web_push_endpoints_Endpoint",
                schema: "public",
                table: "web_push_endpoints",
                column: "Endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_web_push_endpoints_TenantId",
                schema: "public",
                table: "web_push_endpoints",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_web_push_endpoints_UserId",
                schema: "public",
                table: "web_push_endpoints",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_widget_settings_TenantId",
                schema: "public",
                table: "widget_settings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_zones_fallback_unique",
                schema: "public",
                table: "zones",
                column: "TenantId",
                unique: true,
                filter: "\"IsFallback\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_zones_TenantId_Name",
                schema: "public",
                table: "zones",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE "journal_entries"
                ADD COLUMN "search_vector" tsvector
                GENERATED ALWAYS AS (
                    to_tsvector('swedish',
                        coalesce("Anamnes", '') || ' ' ||
                        coalesce("StatusKlinisk", '') || ' ' ||
                        coalesce("Atgarder", '') || ' ' ||
                        coalesce("Diagnos", '') || ' ' ||
                        coalesce("PrognosOchPlan", '')
                    )
                ) STORED;
                CREATE INDEX "idx_journal_entries_search_vector_gin"
                ON "journal_entries" USING GIN ("search_vector");
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "visits"
                ADD COLUMN "TimeRange" tstzrange
                GENERATED ALWAYS AS (tstzrange("StartsAt", "EndsAt", '[)')) STORED;
                ALTER TABLE "visits"
                ADD CONSTRAINT visits_no_overlap
                EXCLUDE USING gist ("PractitionerId" WITH =, "TimeRange" WITH &&)
                WHERE ("OccupiesSlot" = true);
                """);

            migrationBuilder.Sql("""
                CREATE TABLE "consumed_magic_link_tokens" (
                    "TokenHash" varchar(64) PRIMARY KEY,
                    "ConsumedAt" timestamptz NOT NULL DEFAULT now()
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS "consumed_magic_link_tokens";
                ALTER TABLE "visits" DROP CONSTRAINT IF EXISTS visits_no_overlap;
                ALTER TABLE "visits" DROP COLUMN IF EXISTS "TimeRange";
                DROP INDEX IF EXISTS "idx_journal_entries_search_vector_gin";
                ALTER TABLE "journal_entries" DROP COLUMN IF EXISTS "search_vector";
                """);

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims",
                schema: "public");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims",
                schema: "public");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins",
                schema: "public");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles",
                schema: "public");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens",
                schema: "public");

            migrationBuilder.DropTable(
                name: "attachments",
                schema: "public");

            migrationBuilder.DropTable(
                name: "audit_log",
                schema: "public");

            migrationBuilder.DropTable(
                name: "availability_rules",
                schema: "public");

            migrationBuilder.DropTable(
                name: "booking_lines",
                schema: "public");

            migrationBuilder.DropTable(
                name: "in_app_notifications",
                schema: "public");

            migrationBuilder.DropTable(
                name: "journal_amendments",
                schema: "public");

            migrationBuilder.DropTable(
                name: "notification_log",
                schema: "public");

            migrationBuilder.DropTable(
                name: "notification_settings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "notification_templates",
                schema: "public");

            migrationBuilder.DropTable(
                name: "practice_settings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "scheduled_notifications",
                schema: "public");

            migrationBuilder.DropTable(
                name: "time_off",
                schema: "public");

            migrationBuilder.DropTable(
                name: "web_push_endpoints",
                schema: "public");

            migrationBuilder.DropTable(
                name: "widget_settings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "AspNetRoles",
                schema: "public");

            migrationBuilder.DropTable(
                name: "AspNetUsers",
                schema: "public");

            migrationBuilder.DropTable(
                name: "zones",
                schema: "public");

            migrationBuilder.DropTable(
                name: "treatment_types",
                schema: "public");

            migrationBuilder.DropTable(
                name: "visits",
                schema: "public");

            migrationBuilder.DropTable(
                name: "journal_entries",
                schema: "public");

            migrationBuilder.DropTable(
                name: "locations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "horses",
                schema: "public");

            migrationBuilder.DropTable(
                name: "owners",
                schema: "public");

            migrationBuilder.DropTable(
                name: "tenants",
                schema: "public");
        }
    }
}
