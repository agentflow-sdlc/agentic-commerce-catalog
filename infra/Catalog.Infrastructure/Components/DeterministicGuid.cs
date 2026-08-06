using System.Security.Cryptography;
using System.Text;

namespace Catalog.Infrastructure.Components;

internal static class DeterministicGuid
{
    public static string Create(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes).ToString();
    }
}
