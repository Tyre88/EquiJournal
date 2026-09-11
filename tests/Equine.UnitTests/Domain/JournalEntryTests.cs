using Equine.Domain.Entities;
using Equine.Domain.JournalTemplates;
using Shouldly;
using Xunit;

namespace Equine.UnitTests.Domain;

public class JournalEntryTests
{
    [Fact]
    public void Cannot_sign_when_required_fields_are_empty()
    {
        var journal = CreateDraft("", "", "");
        journal.ValidateForSigning().ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => journal.Sign("Victor"));
    }

    [Fact]
    public void Can_update_draft_with_empty_fields()
    {
        var journal = CreateDraft("", "", "");
        journal.UpdateDraft("", "", "", templateDataJson: "{\"vas\":3}");
        journal.Anamnes.ShouldBe("");
        journal.TemplateDataJson.ShouldBe("{\"vas\":3}");
    }

    [Fact]
    public void Cannot_update_after_sign()
    {
        var journal = CreateDraft("ont", "ua", "massage");
        journal.Sign("Victor");
        Should.Throw<InvalidOperationException>(() => journal.UpdateDraft("x", "y", "z"));
    }

    [Fact]
    public void SignImported_allows_empty_clinical_fields_and_keeps_source_date()
    {
        var performed = new DateTimeOffset(2022, 4, 15, 10, 0, 0, TimeSpan.Zero);
        var journal = CreateDraft("", "", "");
        journal.SignImported("Victor (import)", performed);
        journal.Status.ShouldBe(JournalStatus.Signed);
        journal.Source.ShouldBe(JournalSource.Import);
        journal.PerformedAt.ShouldBe(performed);
        journal.SignedAt.ShouldBe(performed);
        journal.SignedBy.ShouldBe("Victor (import)");
        journal.ContentHash.ShouldNotBeNullOrWhiteSpace();
        journal.Anamnes.ShouldBe("");
    }

    [Fact]
    public void SignImported_cannot_run_twice()
    {
        var journal = CreateDraft("ont", "ua", "massage");
        journal.SignImported("Victor (import)", DateTimeOffset.UtcNow);
        Should.Throw<InvalidOperationException>(() =>
            journal.SignImported("Victor (import)", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Content_hash_changes_when_canonical_fields_change()
    {
        var a = CreateDraft("ont i ryggen", "ua", "massage");
        var hashA = a.ComputeContentHash();
        a.UpdateDraft("annan anamnes", "ua", "massage");
        a.ComputeContentHash().ShouldNotBe(hashA);
    }

    [Fact]
    public void Template_validation_accepts_valid_definition()
    {
        var json = """
            {"version":1,"sections":[{"key":"vas","label":"Smärta","type":"number","min":0,"max":10}]}
            """;
        JournalTemplateValidator.TryValidate(json, out var error).ShouldBeTrue();
        error.ShouldBeNull();
    }

    [Fact]
    public void Template_validation_rejects_unknown_type()
    {
        var json = """{"version":1,"sections":[{"key":"x","label":"X","type":"file"}]}""";
        JournalTemplateValidator.TryValidate(json, out var error).ShouldBeFalse();
        error.ShouldContain("unsupported");
    }

    [Fact]
    public void Template_validation_accepts_anatomy_map()
    {
        var json = """
            {"version":1,"sections":[{"key":"palpering","label":"Palpering","type":"anatomy-map","preset":"horse-muscles-standard","findingOptions":["Ua","Öm","Spänd"]}]}
            """;
        JournalTemplateValidator.TryValidate(json, out var error).ShouldBeTrue();
        error.ShouldBeNull();
    }

    [Fact]
    public void Template_validation_accepts_skeleton_anatomy_preset()
    {
        var json = """
            {"version":1,"sections":[{"key":"behandling_karta","label":"Behandling","type":"anatomy-map","preset":"horse-skeleton-standard","findingOptions":["Ua","Öm"]}]}
            """;
        JournalTemplateValidator.TryValidate(json, out var error).ShouldBeTrue();
        error.ShouldBeNull();
    }

    [Fact]
    public void Template_validation_rejects_anatomy_map_with_empty_findings()
    {
        var json = """
            {"version":1,"sections":[{"key":"palpering","label":"Palpering","type":"anatomy-map","preset":"horse-muscles-standard","findingOptions":[]}]}
            """;
        JournalTemplateValidator.TryValidate(json, out var error).ShouldBeFalse();
        error.ShouldContain("findingOptions");
    }

    [Fact]
    public void Template_validation_rejects_unknown_anatomy_preset()
    {
        var json = """
            {"version":1,"sections":[{"key":"palpering","label":"Palpering","type":"anatomy-map","preset":"cow-standard","findingOptions":["Ua"]}]}
            """;
        JournalTemplateValidator.TryValidate(json, out var error).ShouldBeFalse();
        error.ShouldContain("preset");
    }

    private static JournalEntry CreateDraft(string anamnes, string status, string atgarder) =>
        new(
            Guid.CreateVersion7(),
            """{"Name":"Anna"}""",
            "Hast",
            "Sto",
            "2018",
            "chip-1",
            DateTimeOffset.UtcNow,
            anamnes,
            status,
            atgarder,
            createdBy: Guid.CreateVersion7());
}
