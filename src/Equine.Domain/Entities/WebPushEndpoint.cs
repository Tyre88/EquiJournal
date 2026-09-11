using Equine.Domain.Common;

namespace Equine.Domain.Entities;

public class WebPushEndpoint : Entity
{
    public Guid UserId { get; private set; }
    public string Endpoint { get; private set; } = string.Empty;
    public string P256dh { get; private set; } = string.Empty;
    public string Auth { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private WebPushEndpoint() { }

    public WebPushEndpoint(Guid userId, string endpoint, string p256dh, string auth)
        : base(Guid.CreateVersion7())
    {
        UserId = userId;
        Endpoint = endpoint;
        P256dh = p256dh;
        Auth = auth;
    }
}
