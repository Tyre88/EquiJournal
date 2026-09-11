namespace Equine.Infrastructure.Services;

public class PractitionerOptions
{
    public Guid? UserId { get; set; }
    public string Name { get; set; } = "Behandlare";
    public string Clinic { get; set; } = "HästJournal";
    public string Address { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
