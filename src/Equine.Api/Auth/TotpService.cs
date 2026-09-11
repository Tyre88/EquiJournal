using Equine.Infrastructure;

namespace Equine.Api.Auth;

public interface ITotpService
{
    string GenerateProvisioningUri(ApplicationUser user);
    bool VerifyTotp(string secret, string code);
    string GenerateSecretKey();
}

public class TotpService : ITotpService
{
    public string GenerateProvisioningUri(ApplicationUser user)
    {
        var secret = GenerateSecretKey();
        var key = Convert.FromHexString(secret);
        var totp = new OtpNet.Totp(key);
        var provisioningUri = $"otpauth://totp/{Uri.EscapeDataString("Equine.Api")}:{Uri.EscapeDataString(user.Email ?? user.UserName ?? "unknown")}?" +
            $"secret={secret}&issuer=Equine.Api";
        user.TotpSecret = secret;
        return provisioningUri;
    }

    public bool VerifyTotp(string secret, string code)
    {
        var key = Convert.FromHexString(secret);
        var totp = new OtpNet.Totp(key);
        var result = totp.VerifyTotp(code, out var window);
        return result;
    }

    public string GenerateSecretKey()
    {
        var randomBytes = new byte[20];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToHexString(randomBytes);
    }
}
