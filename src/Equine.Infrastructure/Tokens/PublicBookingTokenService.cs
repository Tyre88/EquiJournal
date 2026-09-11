using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace Equine.Infrastructure.Tokens;

public sealed class PublicBookingTokenService
{
    public const string VerifyPurpose = "public-booking-verify";
    public const string ManagePurpose = "public-booking-manage";
    public const string MagicLinkPurpose = "client-magic-link";
    public const string UnsubscribePurpose = "marketing-unsubscribe";

    private readonly IDataProtectionProvider _provider;

    public PublicBookingTokenService(IDataProtectionProvider provider) => _provider = provider;

    public string Protect(Guid lineId, DateTimeOffset expiresAt, string purpose)
    {
        var payload = $"{lineId:N}|{expiresAt.ToUnixTimeSeconds()}";
        var protectedBytes = Encoding.UTF8.GetBytes(_provider.CreateProtector(purpose).Protect(payload));
        return Base64UrlEncode(protectedBytes);
    }

    public bool TryUnprotect(string? token, string purpose, out Guid lineId, out bool expired)
    {
        lineId = Guid.Empty;
        expired = false;
        if (string.IsNullOrWhiteSpace(token)) return false;

        try
        {
            var raw = Encoding.UTF8.GetString(Base64UrlDecode(token));
            var payload = _provider.CreateProtector(purpose).Unprotect(raw);
            var parts = payload.Split('|');
            if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out lineId))
                return false;
            if (!long.TryParse(parts[1], out var unix))
                return false;
            expired = DateTimeOffset.FromUnixTimeSeconds(unix) <= DateTimeOffset.UtcNow;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
