using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Equine.Domain.Entities;
using Equine.Infrastructure;
using Equine.Infrastructure.Email;
using Equine.Infrastructure.Notifications;
using Equine.Infrastructure.Sms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Equine.IntegrationTests;

public class NotificationWorkflowTests : IClassFixture<EquineApiFactory>
{
    private readonly EquineApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public NotificationWorkflowTests(EquineApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Concurrent_dispatch_sends_once()
    {
        using var scope = await _factory.CreateTenantScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<EquineDbContext>();
        var row = new ScheduledNotification(
            NotificationType.MagicLink,
            NotificationChannel.Email,
            "once@example.se",
            "Owner",
            Guid.CreateVersion7(),
            DateTimeOffset.UtcNow.AddMinutes(-1),
            """{"klientnamn":"Anna","kliniknamn":"Test","magiclänk":"https://x"}""");
        db.ScheduledNotifications.Add(row);
        await db.SaveChangesAsync();

        await Task.WhenAll(
            Dispatch(_factory.Services),
            Dispatch(_factory.Services));

        var sender = _factory.Services.GetRequiredService<RecordingEmailSender>();
        sender.Sent.Count(m => m.To == "once@example.se").ShouldBe(1);
    }

    [Fact]
    public async Task Template_unknown_field_is_rejected()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client);
        var settings = await client.GetFromJsonAsync<JsonElement>("/api/app/notification-settings", Json);
        var id = settings.GetProperty("templates")[0].GetProperty("id").GetGuid();
        var res = await client.PutAsJsonAsync($"/api/app/notification-settings/templates/{id}", new
        {
            subject = "Hej",
            body = "Hej {{okäntfält}}"
        });
        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Marketing_excludes_owners_without_consent()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client);
        await client.PostAsJsonAsync("/api/app/owners", new
        {
            name = "Ingen reklam",
            email = $"no-{Guid.NewGuid():N}@ex.se",
            phone = "0700000001",
            marketingConsent = false
        });
        var before = _factory.Services.GetRequiredService<RecordingEmailSender>().Sent.Count;
        var send = await client.PostAsJsonAsync("/api/app/marketing/send", new { subject = "Hej", body = "Erbjudande" });
        send.EnsureSuccessStatusCode();
        var body = await send.Content.ReadFromJsonAsync<JsonElement>(Json);
        body.GetProperty("queued").GetInt32().ShouldBe(0);
        _factory.Services.GetRequiredService<RecordingEmailSender>().Sent.Count.ShouldBe(before);
    }

    [Fact]
    public async Task Magic_link_unknown_and_known_email_look_the_same()
    {
        var client = _factory.CreateClient();
        var unknown = await client.PostAsJsonAsync("/api/public/default/auth/magic-link", new { email = "missing@ex.se" });
        unknown.StatusCode.ShouldBe(HttpStatusCode.OK);
        var known = await client.PostAsJsonAsync("/api/public/default/auth/magic-link", new { email = "victor@gradera.nu" });
        known.StatusCode.ShouldBe(HttpStatusCode.OK);
        var a = await unknown.Content.ReadAsStringAsync();
        var b = await known.Content.ReadAsStringAsync();
        a.ShouldBe(b);
    }

    [Fact]
    public async Task Staging_uses_recording_senders()
    {
        _factory.Services.GetRequiredService<IEmailSender>().ShouldBeOfType<RecordingEmailSender>();
        _factory.Services.GetRequiredService<ISmsSender>().ShouldBeOfType<RecordingSmsSender>();
    }

    [Fact]
    public async Task Follow_up_snooze_removes_from_list()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client);
        var owner = await client.PostAsJsonAsync("/api/app/owners", new
        {
            name = "Uppfölj",
            email = $"fu-{Guid.NewGuid():N}@ex.se",
            phone = "0700000002"
        });
        var ownerId = (await owner.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var horse = await client.PostAsJsonAsync("/api/app/horses", new
        {
            ownerId,
            name = "Följe",
            species = "Hast",
            sex = "Sto",
            birthYear = 2015
        });
        var horseId = (await horse.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        await client.PutAsJsonAsync($"/api/app/horses/{horseId}", new { followUpOverrideDays = 7, name = "Följe", species = "Hast", sex = "Sto", birthYear = 2015 });

        var snooze = await client.PostAsJsonAsync($"/api/app/follow-ups/{horseId}/snooze", new { weeks = 2 });
        snooze.EnsureSuccessStatusCode();
        var list = await client.GetFromJsonAsync<JsonElement>("/api/app/follow-ups", Json);
        list.EnumerateArray().Any(x => x.GetProperty("horseId").GetGuid() == horseId).ShouldBeFalse();
    }

    [Fact]
    public async Task Reschedule_cancels_old_reminder_and_schedules_one_new()
    {
        using var scope = await _factory.CreateTenantScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<EquineDbContext>();
        var scheduler = scope.ServiceProvider.GetRequiredService<INotificationScheduler>();
        var entityId = Guid.CreateVersion7();
        var oldWhen = DateTimeOffset.UtcNow.AddHours(20);
        db.ScheduledNotifications.Add(new ScheduledNotification(
            NotificationType.Reminder24h,
            NotificationChannel.Sms,
            "0701111111",
            nameof(BookingLine),
            entityId,
            oldWhen,
            """{"behandling":"Massage"}"""));
        await db.SaveChangesAsync();

        await scheduler.CancelPendingAsync(NotificationType.Reminder24h, entityId);
        await scheduler.EnqueueAsync(
            NotificationType.Reminder24h,
            NotificationChannel.Sms,
            "0701111111",
            nameof(BookingLine),
            entityId,
            DateTimeOffset.UtcNow.AddHours(30),
            new Dictionary<string, string> { ["behandling"] = "Massage" });

        var pending = await db.ScheduledNotifications
            .Where(n => n.RelatedEntityId == entityId && n.Type == NotificationType.Reminder24h)
            .ToListAsync();
        pending.Count(n => n.Status == NotificationDeliveryStatus.Pending).ShouldBe(1);
        pending.Count(n => n.Status == NotificationDeliveryStatus.Cancelled).ShouldBe(1);
        pending.Single(n => n.Status == NotificationDeliveryStatus.Pending).ScheduledFor.ShouldBeGreaterThan(oldWhen);
    }

    [Fact]
    public async Task Sms_at_0300_is_deferred_to_quiet_hours()
    {
        using var scope = await _factory.CreateTenantScopeAsync();
        var scheduler = scope.ServiceProvider.GetRequiredService<INotificationScheduler>();
        var db = scope.ServiceProvider.GetRequiredService<EquineDbContext>();
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
        var local = new DateTime(2026, 1, 15, 3, 0, 0);
        var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone);
        var entityId = Guid.CreateVersion7();

        await scheduler.EnqueueAsync(
            NotificationType.Reminder24h,
            NotificationChannel.Sms,
            "0702222222",
            nameof(BookingLine),
            entityId,
            new DateTimeOffset(utc, TimeSpan.Zero),
            new Dictionary<string, string> { ["behandling"] = "Massage" });

        var row = await db.ScheduledNotifications.SingleAsync(n => n.RelatedEntityId == entityId);
        var deferredLocal = TimeZoneInfo.ConvertTime(row.ScheduledFor, zone);
        deferredLocal.Hour.ShouldBe(7);
        deferredLocal.Day.ShouldBe(15);
    }

    [Fact]
    public async Task Postmark_webhook_rejects_invalid_signature_and_hard_bounce_flags_owner()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client);
        var email = $"bounce-{Guid.NewGuid():N}@ex.se";
        var created = await client.PostAsJsonAsync("/api/app/owners", new
        {
            name = "Studs",
            email,
            phone = "0703333333"
        });
        created.EnsureSuccessStatusCode();

        using (var scope = await _factory.CreateTenantScopeAsync())
        {
            var db = scope.ServiceProvider.GetRequiredService<EquineDbContext>();
            db.NotificationLog.Add(new NotificationLog(
                NotificationType.MagicLink,
                NotificationChannel.Email,
                email,
                null,
                "Länk",
                "body",
                "Owner",
                Guid.CreateVersion7(),
                "Sent",
                "pm-bounce-1",
                null));
            await db.SaveChangesAsync();
        }

        var bad = await client.PostAsync("/api/public/webhooks/postmark",
            new StringContent("""{"MessageID":"pm-bounce-1","RecordType":"Bounce","Type":"HardBounce"}""", Encoding.UTF8, "application/json"));
        bad.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/public/webhooks/postmark")
        {
            Content = new StringContent("""{"MessageID":"pm-bounce-1","RecordType":"Bounce","Type":"HardBounce"}""", Encoding.UTF8, "application/json")
        };
        req.Headers.Add("X-Postmark-Webhook-Token", "test-postmark-token");
        var ok = await client.SendAsync(req);
        ok.EnsureSuccessStatusCode();

        using var check = await _factory.CreateTenantScopeAsync();
        var owners = check.ServiceProvider.GetRequiredService<EquineDbContext>();
        var owner = await owners.Owners.SingleAsync(o => o.Email == email);
        owner.EmailInvalid.ShouldBeTrue();
    }

    [Fact]
    public async Task Practice_update_is_used_in_outbound_email()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client);
        var clinic = $"Klinik-{Guid.NewGuid():N}"[..20];
        var put = await client.PutAsJsonAsync("/api/app/practice-settings", new
        {
            name = "Behandlare",
            clinic,
            address = "Stallet 1",
            phone = "0700000000",
            email = "victor@gradera.nu"
        });
        put.EnsureSuccessStatusCode();

        var ownerEmail = $"clinic-{Guid.NewGuid():N}@ex.se";
        (await client.PostAsJsonAsync("/api/app/owners", new
        {
            name = "Klinik Kund",
            email = ownerEmail,
            phone = "0704444444"
        })).EnsureSuccessStatusCode();

        (await client.PostAsJsonAsync("/api/public/default/auth/magic-link", new { email = ownerEmail })).EnsureSuccessStatusCode();
        await DispatchDueAsync();
        var sent = _factory.Services.GetRequiredService<RecordingEmailSender>().Sent
            .FirstOrDefault(m => string.Equals(m.To, ownerEmail, StringComparison.OrdinalIgnoreCase));
        sent.ShouldNotBeNull($"recipients: {string.Join(", ", _factory.Services.GetRequiredService<RecordingEmailSender>().Sent.Select(m => m.To))}");
        (sent!.Subject + "\n" + sent.TextBody).ShouldContain(clinic);
    }

    [Fact]
    public async Task Practice_home_address_round_trips()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client);
        var put = await client.PutAsJsonAsync("/api/app/practice-settings", new
        {
            name = "Victor",
            clinic = "HästJournal",
            addressStreet = "Storgatan 1",
            addressPostcode = "27531",
            addressCity = "Sjöbo",
            phone = "0700000000",
            email = "victor@gradera.nu"
        });
        put.EnsureSuccessStatusCode();

        var saved = await put.Content.ReadFromJsonAsync<JsonElement>();
        saved.GetProperty("addressStreet").GetString().ShouldBe("Storgatan 1");
        saved.GetProperty("addressPostcode").GetString().ShouldBe("275 31");
        saved.GetProperty("addressCity").GetString().ShouldBe("Sjöbo");
        saved.GetProperty("address").GetString().ShouldBe("Storgatan 1, 275 31 Sjöbo");

        var get = await client.GetFromJsonAsync<JsonElement>("/api/app/practice-settings");
        get.GetProperty("addressStreet").GetString().ShouldBe("Storgatan 1");
        get.GetProperty("address").GetString().ShouldBe("Storgatan 1, 275 31 Sjöbo");
    }

    [Fact]
    public async Task Widget_snippet_contains_data_treatment()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client);
        var settings = await client.GetFromJsonAsync<JsonElement>("/api/app/widget-settings", Json);
        var snippets = settings.GetProperty("treatmentSnippets");
        if (snippets.GetArrayLength() == 0)
        {
            (await client.PostAsJsonAsync("/api/app/treatment-types", new
            {
                name = $"Online {Guid.NewGuid():N}"[..18],
                shortDescription = "Kropp",
                durationMinutes = 30,
                bookableOnline = true,
                requiresApproval = false
            })).EnsureSuccessStatusCode();
            settings = await client.GetFromJsonAsync<JsonElement>("/api/app/widget-settings", Json);
            snippets = settings.GetProperty("treatmentSnippets");
        }
        snippets.GetArrayLength().ShouldBeGreaterThan(0);
        snippets[0].GetProperty("snippet").GetString().ShouldContain("data-treatment=");
    }

    [Fact]
    public async Task Widget_settings_uses_request_host_when_public_base_is_loopback()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client);
        await PutPublicBaseUrlAsync(client, "http://localhost:5087");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/app/widget-settings");
        request.Headers.Host = "bokning.example.se";
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var settings = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        settings.GetProperty("publicBaseUrl").GetString().ShouldBe("http://bokning.example.se");
        settings.GetProperty("embedSnippet").GetString().ShouldContain("http://bokning.example.se/widget/v1/loader.js");
        settings.GetProperty("embedSnippet").GetString().ShouldContain("data-slug=");
        settings.GetProperty("embedSnippet").GetString().ShouldNotContain("localhost:5087");
    }

    [Fact]
    public async Task Widget_settings_keeps_explicit_public_base_url()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client);
        var saved = await PutPublicBaseUrlAsync(client, "https://widget.example.se");
        saved.GetProperty("publicBaseUrl").GetString().ShouldBe("https://widget.example.se");
        saved.GetProperty("embedSnippet").GetString().ShouldContain("https://widget.example.se/widget/v1/loader.js");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/app/widget-settings");
        request.Headers.Host = "bokning.example.se";
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var settings = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        settings.GetProperty("publicBaseUrl").GetString().ShouldBe("https://widget.example.se");

        await PutPublicBaseUrlAsync(client, "http://localhost:5087");
    }

    [Fact]
    public async Task Widget_csp_allows_same_origin_framing()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/widget/");
        var csp = response.Headers.GetValues("Content-Security-Policy").ShouldHaveSingleItem();
        csp.ShouldStartWith("frame-ancestors 'self'");
    }

    [Fact]
    public async Task Portal_cannot_read_another_owners_horse_and_hides_journal_by_default()
    {
        var admin = _factory.CreateClient();
        await LoginAsync(admin);
        var ownerAEmail = $"porta-{Guid.NewGuid():N}@ex.se";
        var ownerBEmail = $"portb-{Guid.NewGuid():N}@ex.se";
        var ownerA = await admin.PostAsJsonAsync("/api/app/owners", new { name = "Portal A", email = ownerAEmail, phone = "0705555555" });
        var ownerB = await admin.PostAsJsonAsync("/api/app/owners", new { name = "Portal B", email = ownerBEmail, phone = "0706666666" });
        var ownerAId = (await ownerA.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var ownerBId = (await ownerB.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var horseA = await admin.PostAsJsonAsync("/api/app/horses", new { ownerId = ownerAId, name = "A-häst", species = "Hast", sex = "Sto", birthYear = 2014 });
        var horseB = await admin.PostAsJsonAsync("/api/app/horses", new { ownerId = ownerBId, name = "B-häst", species = "Hast", sex = "Valack", birthYear = 2013 });
        var horseAId = (await horseA.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var horseBId = (await horseB.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        ownerA.EnsureSuccessStatusCode();
        ownerB.EnsureSuccessStatusCode();
        horseA.EnsureSuccessStatusCode();
        horseB.EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/public/default/auth/magic-link", new { email = ownerAEmail })).EnsureSuccessStatusCode();
        await DispatchDueAsync();
        var mail = _factory.Services.GetRequiredService<RecordingEmailSender>().Sent
            .FirstOrDefault(m => string.Equals(m.To, ownerAEmail, StringComparison.OrdinalIgnoreCase));
        mail.ShouldNotBeNull($"recipients: {string.Join(", ", _factory.Services.GetRequiredService<RecordingEmailSender>().Sent.Select(m => m.To))}");
        var match = System.Text.RegularExpressions.Regex.Match(mail!.TextBody, @"token=([^&\s]+)");
        match.Success.ShouldBeTrue();

        var portal = _factory.CreateClient();
        var exchange = await portal.PostAsJsonAsync("/api/public/auth/magic-link/exchange", new { token = Uri.UnescapeDataString(match.Groups[1].Value) });
        exchange.EnsureSuccessStatusCode();
        var access = (await exchange.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("accessToken").GetString();
        portal.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", access);

        var foreign = await portal.GetAsync($"/api/app/portal/horses/{horseBId}/summary");
        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var own = await portal.GetFromJsonAsync<JsonElement>($"/api/app/portal/horses/{horseAId}/summary", Json);
        own.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Unsigned_journal_scanner_enqueues_practitioner_mail()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client);
        var owner = await client.PostAsJsonAsync("/api/app/owners", new
        {
            name = "Journalägare",
            email = $"ju-{Guid.NewGuid():N}@ex.se",
            phone = "0707777777"
        });
        var ownerId = (await owner.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var horse = await client.PostAsJsonAsync("/api/app/horses", new
        {
            ownerId,
            name = "Utkast",
            species = "Hast",
            sex = "Sto",
            birthYear = 2012
        });
        var horseId = (await horse.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var journal = await client.PostAsJsonAsync("/api/app/journals", new
        {
            horseId,
            performedAt = DateTimeOffset.UtcNow,
            anamnes = "öm",
            statusKlinisk = "status",
            atgarder = "åtgärd"
        });
        journal.EnsureSuccessStatusCode();
        var journalId = (await journal.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        using var scope = await _factory.CreateTenantScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<EquineDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE journal_entries SET \"CreatedAt\" = {DateTimeOffset.UtcNow.AddHours(-25)} WHERE \"Id\" = {journalId}");
        var jobs = scope.ServiceProvider.GetRequiredService<Equine.Jobs.NotificationJobs>();
        await jobs.ScanUnsignedJournals();
        await DispatchDueAsync();
        var sender = _factory.Services.GetRequiredService<RecordingEmailSender>();
        sender.Sent.ShouldContain(m => m.Subject.Contains("Osignerad", StringComparison.OrdinalIgnoreCase));
        var unsignedMailCount = sender.Sent.Count(m => m.Subject.Contains("Osignerad", StringComparison.OrdinalIgnoreCase));

        await jobs.ScanUnsignedJournals();
        await DispatchDueAsync();
        sender.Sent.Count(m => m.Subject.Contains("Osignerad", StringComparison.OrdinalIgnoreCase))
            .ShouldBe(unsignedMailCount);
    }

    private async Task DispatchDueAsync()
    {
        using var scope = await _factory.CreateTenantScopeAsync();
        await scope.ServiceProvider.GetRequiredService<NotificationDispatcher>().DispatchDueAsync();
    }

    private static async Task Dispatch(IServiceProvider root)
    {
        using var scope = root.CreateScope();
        await EquineApiFactory.BindDefaultTenantAsync(scope.ServiceProvider);
        await scope.ServiceProvider.GetRequiredService<NotificationDispatcher>().DispatchDueAsync();
    }

    private static async Task LoginAsync(HttpClient client)
    {
        var login = await client.PostAsJsonAsync("/api/app/auth/login", new { email = "victor@gradera.nu", password = "Admin@123456" });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<JsonElement>(Json);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
    }

    private static async Task<JsonElement> PutPublicBaseUrlAsync(HttpClient client, string publicBaseUrl)
    {
        var current = await client.GetFromJsonAsync<JsonElement>("/api/app/widget-settings", Json);
        current.ValueKind.ShouldNotBe(JsonValueKind.Undefined);
        var put = await client.PutAsJsonAsync("/api/app/widget-settings", new
        {
            allowedOrigins = current.GetProperty("allowedOrigins").EnumerateArray().Select(x => x.GetString()).ToArray(),
            showPrices = current.GetProperty("showPrices").GetBoolean(),
            bookingTerms = current.GetProperty("bookingTerms").GetString(),
            privacyPolicyUrl = current.GetProperty("privacyPolicyUrl").GetString(),
            verificationWindowMinutes = current.GetProperty("verificationWindowMinutes").GetInt32(),
            cancellationNoticeHours = current.GetProperty("cancellationNoticeHours").GetInt32(),
            publicBaseUrl,
            contactPhone = current.GetProperty("contactPhone").GetString(),
            contactEmail = current.GetProperty("contactEmail").GetString()
        });
        put.EnsureSuccessStatusCode();
        return await put.Content.ReadFromJsonAsync<JsonElement>(Json);
    }
}
