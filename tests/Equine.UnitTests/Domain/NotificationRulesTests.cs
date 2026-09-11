using Equine.Domain.Notifications;
using Equine.Infrastructure.Email;
using Equine.Infrastructure.Notifications;
using Shouldly;
using Xunit;

namespace Equine.UnitTests.Domain;

public class NotificationRulesTests
{
    [Fact]
    public void MergeFields_reject_unknown()
    {
        MergeFields.UnknownFields("Hej {{klientnamn}} {{okäntfält}}").ShouldContain("okäntfält");
        MergeFields.UnknownFields("Hej {{klientnamn}}").ShouldBeEmpty();
    }

    [Fact]
    public void QuietHours_defers_0300_to_window_start()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
        var local = new DateTime(2026, 1, 15, 3, 0, 0);
        var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone);
        var deferred = QuietHours.DeferToWindow(new DateTimeOffset(utc, TimeSpan.Zero), new TimeOnly(7, 0), new TimeOnly(21, 0), zone);
        var deferredLocal = TimeZoneInfo.ConvertTime(deferred, zone);
        deferredLocal.Hour.ShouldBe(7);
        deferredLocal.Day.ShouldBe(15);
    }

    [Fact]
    public void QuietHours_defers_across_dst_spring_forward()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
        var local = new DateTime(2026, 3, 29, 3, 0, 0);
        var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone);
        var deferred = QuietHours.DeferToWindow(new DateTimeOffset(utc, TimeSpan.Zero), new TimeOnly(7, 0), new TimeOnly(21, 0), zone);
        var deferredLocal = TimeZoneInfo.ConvertTime(deferred, zone);
        deferredLocal.Hour.ShouldBe(7);
    }

    [Fact]
    public void Ics_sequence_increments()
    {
        var uid = Guid.Parse("aaaaaaaa-bbbb-7ccc-dddd-eeeeeeeeeeee");
        var start = new DateTimeOffset(2026, 6, 15, 8, 0, 0, TimeSpan.Zero);
        var first = System.Text.Encoding.UTF8.GetString(IcsBuilder.Build(uid, start, start.AddMinutes(30), "Massage", "Stall", "x", 0));
        var second = System.Text.Encoding.UTF8.GetString(IcsBuilder.Build(uid, start.AddHours(1), start.AddHours(1).AddMinutes(30), "Massage", "Stall", "x", 1));
        first.ShouldContain("SEQUENCE:0");
        second.ShouldContain("SEQUENCE:1");
        first.ShouldContain("UID:aaaaaaaabbbb7cccddddeeeeeeeeeeee@hastjournal");
        second.ShouldContain("UID:aaaaaaaabbbb7cccddddeeeeeeeeeeee@hastjournal");
    }

    [Fact]
    public async Task Dns_checker_skips_localhost()
    {
        var checker = new DnsDeliverabilityChecker();
        var result = await checker.CheckAsync("localhost");
        result.Passed.ShouldBeTrue();
    }

    [Fact]
    public async Task Dns_checker_fails_closed_when_spf_missing()
    {
        var checker = new DnsDeliverabilityChecker();
        var result = await checker.CheckAsync("no-spf.invalid");
        result.Passed.ShouldBeFalse();
    }
}
