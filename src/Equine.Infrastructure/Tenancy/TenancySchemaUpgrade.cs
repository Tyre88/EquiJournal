using Microsoft.EntityFrameworkCore;

namespace Equine.Infrastructure.Tenancy;

public static class TenancySchemaUpgrade
{
    private static readonly string[] TenantTables =
    [
        "owners", "horses", "treatment_types", "journal_entries", "journal_amendments",
        "attachments", "locations", "zones", "availability_rules", "time_off", "visits",
        "booking_lines", "widget_settings", "practice_settings", "notification_settings",
        "notification_templates", "scheduled_notifications", "notification_log",
        "in_app_notifications", "web_push_endpoints", "audit_log"
    ];

    public static async Task ApplyAsync(EquineDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS tenants (
                "Id" uuid PRIMARY KEY,
                "Name" varchar(200) NOT NULL,
                "Slug" citext NOT NULL,
                "Status" varchar(20) NOT NULL,
                "Plan" varchar(20) NOT NULL,
                "SubscriptionStatus" varchar(20) NOT NULL,
                "CreatedAt" timestamptz NOT NULL DEFAULT now()
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_tenants_slug ON tenants ("Slug");
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO tenants ("Id", "Name", "Slug", "Status", "Plan", "SubscriptionStatus", "CreatedAt")
            SELECT gen_random_uuid(),
                   COALESCE(NULLIF((SELECT "Clinic" FROM practice_settings LIMIT 1), ''), 'HästJournal'),
                   'default',
                   'Active',
                   'Free',
                   'Free',
                   now()
            WHERE NOT EXISTS (SELECT 1 FROM tenants);
            """, cancellationToken);

        foreach (var table in TenantTables)
        {
            await db.Database.ExecuteSqlRawAsync($"""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'public' AND table_name = '{table}'
                    ) THEN
                        ALTER TABLE "{table}" ADD COLUMN IF NOT EXISTS "TenantId" uuid NULL;
                    END IF;
                END $$;
                """, cancellationToken);
        }

        await db.Database.ExecuteSqlRawAsync("""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = 'AspNetUsers'
                ) THEN
                    ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "TenantId" uuid NULL;
                END IF;
            END $$;
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            DO $$
            DECLARE tenant_id uuid;
            BEGIN
                SELECT "Id" INTO tenant_id FROM tenants ORDER BY "CreatedAt" LIMIT 1;
                IF tenant_id IS NULL THEN
                    RETURN;
                END IF;

                UPDATE owners SET "TenantId" = tenant_id WHERE "TenantId" IS NULL;
                UPDATE horses SET "TenantId" = tenant_id WHERE "TenantId" IS NULL;
                UPDATE treatment_types SET "TenantId" = tenant_id WHERE "TenantId" IS NULL;
                UPDATE journal_entries SET "TenantId" = tenant_id WHERE "TenantId" IS NULL;
                UPDATE journal_amendments SET "TenantId" = tenant_id WHERE "TenantId" IS NULL;
                UPDATE attachments SET "TenantId" = tenant_id WHERE "TenantId" IS NULL;
                UPDATE locations SET "TenantId" = tenant_id WHERE "TenantId" IS NULL;
                UPDATE zones SET "TenantId" = tenant_id WHERE "TenantId" IS NULL;
                UPDATE availability_rules SET "TenantId" = tenant_id WHERE "TenantId" IS NULL;
                UPDATE time_off SET "TenantId" = tenant_id WHERE "TenantId" IS NULL;
                UPDATE visits SET "TenantId" = tenant_id WHERE "TenantId" IS NULL;
                UPDATE booking_lines SET "TenantId" = tenant_id WHERE "TenantId" IS NULL;
                UPDATE widget_settings SET "TenantId" = tenant_id WHERE "TenantId" IS NULL;
                UPDATE practice_settings SET "TenantId" = tenant_id WHERE "TenantId" IS NULL;
                UPDATE notification_settings SET "TenantId" = tenant_id WHERE "TenantId" IS NULL;
                UPDATE notification_templates SET "TenantId" = tenant_id WHERE "TenantId" IS NULL;
                UPDATE scheduled_notifications SET "TenantId" = tenant_id WHERE "TenantId" IS NULL;
                UPDATE notification_log SET "TenantId" = tenant_id WHERE "TenantId" IS NULL;
                UPDATE in_app_notifications SET "TenantId" = tenant_id WHERE "TenantId" IS NULL;
                UPDATE web_push_endpoints SET "TenantId" = tenant_id WHERE "TenantId" IS NULL;
                UPDATE audit_log SET "TenantId" = tenant_id WHERE "TenantId" IS NULL;
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'AspNetUsers') THEN
                    UPDATE "AspNetUsers" SET "TenantId" = tenant_id WHERE "TenantId" IS NULL;
                END IF;
            END $$;
            """, cancellationToken);

        foreach (var table in TenantTables)
        {
            await db.Database.ExecuteSqlRawAsync($"""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public' AND table_name = '{table}' AND column_name = 'TenantId'
                    ) THEN
                        ALTER TABLE "{table}" ALTER COLUMN "TenantId" SET NOT NULL;
                    END IF;
                END $$;
                """, cancellationToken);
        }

        await db.Database.ExecuteSqlRawAsync("""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'AspNetUsers' AND column_name = 'TenantId'
                ) THEN
                    ALTER TABLE "AspNetUsers" ALTER COLUMN "TenantId" SET NOT NULL;
                END IF;
            END $$;

            DROP INDEX IF EXISTS ix_owners_email_unique;
            CREATE UNIQUE INDEX IF NOT EXISTS ix_owners_email_unique
                ON owners ("TenantId", "Email") WHERE "DeletedAt" IS NULL;

            DROP INDEX IF EXISTS "IX_treatment_types_Slug";
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_treatment_types_TenantId_Slug"
                ON treatment_types ("TenantId", "Slug");

            DROP INDEX IF EXISTS "IX_zones_Name";
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_zones_TenantId_Name" ON zones ("TenantId", "Name");
            DROP INDEX IF EXISTS "IX_zones_IsFallback";
            CREATE UNIQUE INDEX IF NOT EXISTS ix_zones_fallback_unique
                ON zones ("TenantId") WHERE "IsFallback" = TRUE;

            DROP INDEX IF EXISTS "IX_notification_templates_Type_Channel";
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_notification_templates_TenantId_Type_Channel"
                ON notification_templates ("TenantId", "Type", "Channel");

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_practice_settings_TenantId" ON practice_settings ("TenantId");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_widget_settings_TenantId" ON widget_settings ("TenantId");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_notification_settings_TenantId" ON notification_settings ("TenantId");
            """, cancellationToken);
    }
}
