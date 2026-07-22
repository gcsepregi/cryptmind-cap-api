using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CryptMindCapAPI.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace CryptMindCapAPI.Core.Services;

public record FlagsResponse(SortedDictionary<string, bool> Flags, string Etag, string Entitlement_id);

public sealed class FeaturesService(AppSettings settings, FlagsDbContext db)
{
    private static readonly IReadOnlyDictionary<string, bool> DefaultFlags =
        new SortedDictionary<string, bool>(StringComparer.Ordinal)
        {
            ["journal.core"] = true,
            ["mystweld.core"] = true,
            ["test.core"] = false,
        };

    public async Task<SortedDictionary<string, bool>> GetEffectiveFlagsAsync(string entitlementId)
    {
        var eid = NormalizeEntitlementId(entitlementId);
        var flags = new SortedDictionary<string, bool>(
            DefaultFlags.ToDictionary(kv => kv.Key, kv => kv.Value),
            StringComparer.Ordinal);

        var overrides = await db.Overrides
            .Where(o => o.EntitlementId == eid)
            .ToListAsync();

        foreach (var o in overrides)
        {
            flags[o.FlagKey] = o.Value;
        }

        return flags;
    }

    public static string EtagFor(SortedDictionary<string, bool> flags)
    {
        var json = JsonSerializer.Serialize(flags);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string NormalizeEntitlementId(string eid)
    {
        eid = (eid ?? "").Trim();
        switch (eid.Length)
        {
            case 0:
                throw new ArgumentException("Empty entitlement ID");
            case > 256:
            {
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(eid));
                eid = "u_" + Convert.ToHexString(hash).ToLowerInvariant();
                break;
            }
        }
        return eid;
    }
}
