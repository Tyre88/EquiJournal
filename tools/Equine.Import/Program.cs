using System.Text.Json;
using Equine.Infrastructure;
using Equine.Infrastructure.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var commit = args.Contains("--commit", StringComparer.OrdinalIgnoreCase);
var dryRun = !commit || args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
if (commit)
    dryRun = false;

string? Arg(string name)
{
    var i = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

var ownersPath = Arg("--owners");
var horsesPath = Arg("--horses");
var journalsPath = Arg("--journals");
var reportPath = Arg("--report") ?? "import-report.json";
var signedBy = Arg("--signed-by");
var connection = Arg("--connection");

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase) || ownersPath is null)
{
    Console.WriteLine("""
        Equine historic CSV import. Dry-run is the default; pass --commit to write.

        dotnet run --project tools/Equine.Import -- --owners owners.csv --horses horses.csv --journals journals.csv
        dotnet run --project tools/Equine.Import -- --owners owners.csv --horses horses.csv --journals journals.csv --commit

        Optional: --report import-report.json --signed-by "Namn (import)" --connection "Host=..."
        """);
    return ownersPath is null ? 1 : 0;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables();

connection ??= builder.Configuration.GetConnectionString("Default")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
    ?? throw new InvalidOperationException("Set ConnectionStrings__Default or --connection.");

builder.Services.AddDbContext<EquineDbContext>(o => o.UseNpgsql(connection));
using var host = builder.Build();
using var scope = host.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<EquineDbContext>();

var owners = SemicolonCsv.ReadFile(ownersPath);
var horses = horsesPath is null ? [] : SemicolonCsv.ReadFile(horsesPath);
var journals = journalsPath is null ? [] : SemicolonCsv.ReadFile(journalsPath);

var importer = new HistoricImportService(db);
var report = await importer.ImportAsync(owners, horses, journals, commit: !dryRun, signedBy);

var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
await File.WriteAllTextAsync(reportPath, json);

Console.WriteLine(dryRun ? "DRY-RUN — nothing written." : "COMMIT — rows persisted.");
Console.WriteLine($"Owners in/created: {report.OwnersIn}/{report.OwnersCreated}");
Console.WriteLine($"Horses in/created: {report.HorsesIn}/{report.HorsesCreated}");
Console.WriteLine($"Journals in/created: {report.JournalsIn}/{report.JournalsCreated}");
Console.WriteLine($"Skipped: {report.Skipped.Count}");
foreach (var skip in report.Skipped)
    Console.WriteLine($"  {skip.Entity} row {skip.Row}: {skip.Reason} {skip.Key}");
Console.WriteLine($"Report: {Path.GetFullPath(reportPath)}");
return 0;
