namespace Equine.Api.Auth;

public record LoginRequest(string Email, string Password);
public record LoginResponse(string AccessToken, string RefreshToken, DateTimeOffset Expires);
public record RefreshTokenRequest(string AccessToken, string RefreshToken);
