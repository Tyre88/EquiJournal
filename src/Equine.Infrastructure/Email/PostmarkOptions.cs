namespace Equine.Infrastructure.Email;

public class PostmarkOptions
{
    public string ServerToken { get; set; } = string.Empty;
    public string From { get; set; } = "bokning@localhost";
    public string FromName { get; set; } = "HästJournal";
    public string? ReplyTo { get; set; }
    public string? MessageStream { get; set; } = "outbound";
}
