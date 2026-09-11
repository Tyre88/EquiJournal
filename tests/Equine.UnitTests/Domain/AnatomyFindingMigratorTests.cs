using System.Text.Json;
using Equine.Domain.Entities;
using Equine.Domain.JournalTemplates;
using Shouldly;
using Xunit;

namespace Equine.UnitTests.Domain;

public class AnatomyFindingMigratorTests
{
    [Fact]
    public void Legacy_default_template_is_upgraded_to_current_default()
    {
        var json = """
            {"version":1,"sections":[{"key":"palpering","label":"Palpering","type":"anatomy-map","preset":"horse-muscles-standard","findingOptions":["Ua","Öm","Spänd","Svullnad"]}]}
            """;

        var migrated = AnatomyFindingMigrator.TryMigrateTemplateJson(json);

        migrated.ShouldNotBeNull();
        var options = ReadFindingOptions(migrated);
        options.ShouldBe(FindingOptionsDefaults.CurrentDefault);
    }

    [Fact]
    public void Custom_template_only_renames_Svullnad()
    {
        var json = """
            {"version":1,"sections":[{"key":"palpering","label":"Palpering","type":"anatomy-map","preset":"horse-muscles-standard","findingOptions":["Ua","Svullnad","Galla"]}]}
            """;

        var migrated = AnatomyFindingMigrator.TryMigrateTemplateJson(json);

        migrated.ShouldNotBeNull();
        ReadFindingOptions(migrated).ShouldBe(["Ua", "Svullen", "Galla"]);
    }

    [Fact]
    public void Journal_annotation_finding_is_renamed()
    {
        var json = """
            {"palpering":{"preset":"horse-muscles-standard","annotations":[{"regionId":"neck-l","label":"Hals","side":"L","finding":"Svullnad","note":""}],"strokes":[]}}
            """;

        var migrated = AnatomyFindingMigrator.TryMigrateJournalDataJson(json);

        migrated.ShouldNotBeNull();
        ReadFirstFinding(migrated).ShouldBe("Svullen");
    }

    [Fact]
    public void Migrator_is_idempotent()
    {
        var template = """
            {"version":1,"sections":[{"key":"palpering","label":"Palpering","type":"anatomy-map","preset":"horse-muscles-standard","findingOptions":["Ua","Öm","Spänd","Svullnad"]}]}
            """;
        var journal = """
            {"palpering":{"preset":"horse-muscles-standard","annotations":[{"regionId":"neck-l","label":"Hals","side":"L","finding":"Svullnad","note":""}],"strokes":[]}}
            """;

        var migratedTemplate = AnatomyFindingMigrator.TryMigrateTemplateJson(template);
        var migratedJournal = AnatomyFindingMigrator.TryMigrateJournalDataJson(journal);

        AnatomyFindingMigrator.TryMigrateTemplateJson(migratedTemplate).ShouldBeNull();
        AnatomyFindingMigrator.TryMigrateJournalDataJson(migratedJournal).ShouldBeNull();
    }

    [Fact]
    public void Signed_journal_hash_matches_after_migration()
    {
        var original = """
            {"palpering":{"preset":"horse-muscles-standard","annotations":[{"regionId":"neck-l","label":"Hals","side":"L","finding":"Svullnad","note":""}],"strokes":[]}}
            """;
        var journal = new JournalEntry(
            Guid.CreateVersion7(),
            """{"Name":"Anna"}""",
            "Hast",
            "Sto",
            "2018",
            "chip-1",
            DateTimeOffset.UtcNow,
            "ont",
            "ua",
            "massage",
            templateDataJson: original,
            createdBy: Guid.CreateVersion7());
        journal.Sign("Victor");
        var hashBefore = journal.ContentHash;

        var migrated = AnatomyFindingMigrator.TryMigrateJournalDataJson(journal.TemplateDataJson);
        migrated.ShouldNotBeNull();
        journal.TryApplyTemplateDataMigration(migrated).ShouldBeTrue();

        ReadFirstFinding(journal.TemplateDataJson).ShouldBe("Svullen");
        journal.ContentHash.ShouldNotBe(hashBefore);
        journal.ContentHash.ShouldBe(journal.ComputeContentHash());
    }

    private static string[] ReadFindingOptions(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("sections")[0]
            .GetProperty("findingOptions")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToArray();
    }

    private static string ReadFirstFinding(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .EnumerateObject().First().Value
            .GetProperty("annotations")[0]
            .GetProperty("finding")
            .GetString()!;
    }
}
