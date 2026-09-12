using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Equine.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Equine.IntegrationTests;

public class AuthSessionTests : IClassFixture<EquineApiFactory>
{
    private readonly EquineApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public AuthSessionTests(EquineApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_me_and_refresh_issue_new_tokens()
    {
        var client = _factory.CreateClient();
        var login = await LoginAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var me = await client.GetAsync("/api/app/me");
        me.StatusCode.ShouldBe(HttpStatusCode.OK);

        var refreshed = await RefreshAsync(client, login.AccessToken, login.RefreshToken);
        refreshed.AccessToken.ShouldNotBeNullOrWhiteSpace();
        refreshed.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        refreshed.RefreshToken.ShouldNotBe(login.RefreshToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);
        (await client.GetAsync("/api/app/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_with_wrong_token_is_unauthorized_and_does_not_rotate()
    {
        var client = _factory.CreateClient();
        var login = await LoginAsync(client);

        var wrong = await client.PostAsJsonAsync("/api/app/auth/refresh", new
        {
            accessToken = login.AccessToken,
            refreshToken = "not-the-refresh-token"
        });
        wrong.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var stillValid = await RefreshAsync(client, login.AccessToken, login.RefreshToken);
        stillValid.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Rotated_refresh_token_cannot_be_reused()
    {
        var client = _factory.CreateClient();
        var login = await LoginAsync(client);
        await RefreshAsync(client, login.AccessToken, login.RefreshToken);

        var reuse = await client.PostAsJsonAsync("/api/app/auth/refresh", new
        {
            accessToken = login.AccessToken,
            refreshToken = login.RefreshToken
        });
        reuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Expired_refresh_token_is_unauthorized()
    {
        var client = _factory.CreateClient();
        var login = await LoginAsync(client);
        await ExpireRefreshTokenAsync();

        var expired = await client.PostAsJsonAsync("/api/app/auth/refresh", new
        {
            accessToken = login.AccessToken,
            refreshToken = login.RefreshToken
        });
        expired.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Successful_refresh_slides_expiry_forward()
    {
        var client = _factory.CreateClient();
        var login = await LoginAsync(client);
        await SetRefreshExpiryAsync(DateTimeOffset.UtcNow.AddHours(1));

        await RefreshAsync(client, login.AccessToken, login.RefreshToken);

        var expiry = await ReadRefreshExpiryAsync();
        expiry.ShouldNotBeNull();
        expiry.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddHours(3));
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        var client = _factory.CreateClient();
        var login = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        (await client.PostAsync("/api/app/auth/logout", null)).EnsureSuccessStatusCode();

        var afterLogout = await client.PostAsJsonAsync("/api/app/auth/refresh", new
        {
            accessToken = login.AccessToken,
            refreshToken = login.RefreshToken
        });
        afterLogout.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<Tokens> LoginAsync(HttpClient client)
    {
        var login = await client.PostAsJsonAsync("/api/app/auth/login", new
        {
            email = "victor@gradera.nu",
            password = "Admin@123456"
        });
        login.EnsureSuccessStatusCode();
        var payload = await login.Content.ReadFromJsonAsync<JsonElement>(Json);
        return new Tokens(
            payload.GetProperty("accessToken").GetString()!,
            payload.GetProperty("refreshToken").GetString()!);
    }

    private static async Task<Tokens> RefreshAsync(HttpClient client, string accessToken, string refreshToken)
    {
        var res = await client.PostAsJsonAsync("/api/app/auth/refresh", new { accessToken, refreshToken });
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        return new Tokens(
            payload.GetProperty("accessToken").GetString()!,
            payload.GetProperty("refreshToken").GetString()!);
    }

    private async Task ExpireRefreshTokenAsync() =>
        await SetRefreshExpiryAsync(DateTimeOffset.UtcNow.AddMinutes(-5));

    private async Task SetRefreshExpiryAsync(DateTimeOffset expiry)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EquineDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == "victor@gradera.nu");
        user.RefreshTokenExpiresAt = expiry;
        await db.SaveChangesAsync();
    }

    private async Task<DateTimeOffset?> ReadRefreshExpiryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EquineDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == "victor@gradera.nu");
        return user.RefreshTokenExpiresAt;
    }

    private sealed record Tokens(string AccessToken, string RefreshToken);
}
