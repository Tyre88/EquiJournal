namespace Equine.Domain.Entities;

public class JournalAmendment
{
    public Guid Id { get; private set; }
    public Guid JournalEntryId { get; private set; }
    public JournalEntry JournalEntry { get; private set; } = null!;
    public string Text { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private JournalAmendment() { }

    public JournalAmendment(
        Guid journalEntryId,
        string text,
        string reason,
        Guid createdBy = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason cannot be empty.");

        JournalEntryId = journalEntryId;
        Text = text;
        Reason = reason;
        CreatedBy = createdBy == default ? Guid.CreateVersion7() : createdBy;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
