namespace Equine.Infrastructure.Email;

public class SmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public bool UseSsl { get; set; }
    public string From { get; set; } = "bokning@localhost";
    public string FromName { get; set; } = "HästJournal";
}
