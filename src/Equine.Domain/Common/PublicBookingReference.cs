namespace Equine.Domain.Common;

public static class PublicBookingReference
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string Create()
    {
        var bytes = Guid.CreateVersion7().ToByteArray();
        Span<char> chars = stackalloc char[6];
        for (var i = 0; i < 6; i++)
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        return $"HJ-{new string(chars)}";
    }
}
