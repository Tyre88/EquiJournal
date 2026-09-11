namespace Equine.Domain.Entities;

public class Attachment
{
    public Guid Id { get; private set; }
    public Guid JournalEntryId { get; private set; }
    public JournalEntry JournalEntry { get; private set; } = null!;
    public string StorageKey { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long Size { get; private set; }
    public string Sha256Checksum { get; private set; } = string.Empty;
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Attachment() { }

    public Attachment(
        Guid journalEntryId,
        string storageKey,
        string originalFileName,
        string contentType,
        long size,
        string sha256Checksum,
        Guid createdBy = default)
    {
        JournalEntryId = journalEntryId;
        StorageKey = storageKey ?? throw new ArgumentNullException(nameof(storageKey));
        OriginalFileName = originalFileName ?? throw new ArgumentNullException(nameof(originalFileName));
        ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
        Size = size;
        Sha256Checksum = sha256Checksum ?? throw new ArgumentNullException(nameof(sha256Checksum));
        CreatedBy = createdBy == default ? Guid.CreateVersion7() : createdBy;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
