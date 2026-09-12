using Equine.Api.Endpoints;
using Equine.Domain.Common;
using Equine.Domain.Entities;
using Equine.Infrastructure;
using Equine.Infrastructure.Practice;
using Equine.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Equine.Api.Features.Tenants;

public static class TenantRegistrationEndpoints
{
    public static IEndpointRouteBuilder MapTenantRegistrationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/public/tenants/register", async (
            [FromBody] RegisterTenantRequest request,
            EquineDbContext db,
            ITenantContext tenantContext,
            TenantProvisioningService provision,
            UserManager<ApplicationUser> users,
            PracticeSettingsService practice,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var name = (request.Name ?? "").Trim();
            var email = (request.Email ?? "").Trim();
            var password = request.Password ?? "";
            var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? name : request.DisplayName.Trim();
            if (name.Length < 2)
                return Results.BadRequest(new { error = "Ange verksamhetens namn." });
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                return Results.BadRequest(new { error = "Ange en giltig e-postadress." });
            if (password.Length < 12)
                return Results.BadRequest(new { error = "Lösenordet måste vara minst 12 tecken." });

            var slug = TenantSlug.FromName(string.IsNullOrWhiteSpace(request.Slug) ? name : request.Slug);
            if (await db.Tenants.AnyAsync(t => t.Slug == slug, ct))
                return Results.Conflict(new { error = "Adressen är upptagen. Välj ett annat namn." });
            if (await users.FindByEmailAsync(email) is not null)
                return Results.Conflict(new { error = "E-postadressen används redan." });

            var tenant = new Tenant(name, slug);
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(ct);

            tenantContext.SetTenant(tenant.Id, tenant.Slug);

            var user = new ApplicationUser
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Id,
                Email = email,
                UserName = email,
                DisplayName = displayName,
                EmailConfirmed = true,
                IsFirstLogin = true,
                CreatedAt = DateTimeOffset.UtcNow,
                NormalizedEmail = email.ToUpperInvariant(),
                NormalizedUserName = email.ToUpperInvariant()
            };

            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                db.Tenants.Remove(tenant);
                await db.SaveChangesAsync(ct);
                return Results.BadRequest(new { error = string.Join(" ", created.Errors.Select(e => e.Description)) });
            }

            await users.AddToRoleAsync(user, "Admin");
            await provision.SeedDefaultsAsync(ct);

            var settings = await practice.GetAsync(ct);
            settings.Update(
                settings.Name,
                tenant.Name,
                settings.Address,
                settings.AddressStreet,
                settings.AddressPostcode,
                settings.AddressCity,
                settings.Latitude,
                settings.Longitude,
                settings.Phone,
                email,
                settings.VehicleRegistrationNumber);
            await db.SaveChangesAsync(ct);

            var tokens = await AuthEndpoints.IssueTokensForUser(user, users, configuration);
            return Results.Ok(new
            {
                accessToken = tokens.AccessToken,
                refreshToken = tokens.RefreshToken,
                expires = tokens.Expires,
                tenant = new { tenant.Id, tenant.Name, tenant.Slug, plan = tenant.Plan.ToString() }
            });
        })
        .AllowAnonymous()
        .RequireCors("Public")
        .RequireRateLimiting("public-book")
        .WithName("RegisterTenant");

        return app;
    }
}

public sealed record RegisterTenantRequest(
    string? Name,
    string? Slug,
    string? Email,
    string? Password,
    string? DisplayName);
