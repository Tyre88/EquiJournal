namespace Equine.Api.Auth;

public record LoginRequest(string Email, string Password);
public record LoginResponse(string AccessToken, string RefreshToken, DateTimeOffset Expires);
public record RefreshTokenRequest(string AccessToken, string RefreshToken);
public record VerifyTotpRequest(string Code);
public record EnrolTotpResponse(string ProvisioningUri, string Secret);
