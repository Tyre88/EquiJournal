namespace Equine.Api.Features.Schema;

public static class SchemaEndpoints
{
    public static IEndpointRouteBuilder MapSchemaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app/schema").RequireAuthorization("CanManageBookings");

        group.MapGet("/today", async (
            SchemaDayService schema,
            DateOnly? date,
            CancellationToken cancellationToken) =>
        {
            var day = await schema.GetAsync(date, cancellationToken);
            return Results.Ok(day);
        }).WithName("GetTodaysSchema");

        return app;
    }
}
