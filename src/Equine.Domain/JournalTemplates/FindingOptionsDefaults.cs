namespace Equine.Domain.JournalTemplates;

public static class FindingOptionsDefaults
{
    public const string LegacyFindingLabel = "Svullnad";
    public const string CurrentFindingLabel = "Svullen";

    public static readonly string[] LegacyDefault = ["Ua", "Öm", "Spänd", LegacyFindingLabel];

    public static readonly string[] CurrentDefault =
    [
        "Ua", "Öm", "Spänd", CurrentFindingLabel,
        "Galla", "Triggerpunkt", "Sår", "Knöl", "Muskelknuta"
    ];
}
