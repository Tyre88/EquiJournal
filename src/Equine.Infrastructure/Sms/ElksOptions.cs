namespace Equine.Infrastructure.Sms;

public class ElksOptions
{
    public string ApiUrl { get; set; } = "https://api.46elks.com/a1/sms";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = "HastJournal";
}
