using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CryptMindCapAPI.Core;
using Dapper;
using MySqlConnector;

namespace CryptMindCapAPI.Apps.Mystweld.Services;

public record FlagsResponse(SortedDictionary<string, bool> Flags, string Etag);

public sealed class FeaturesService(AppSettings settings)
{
    private static readonly IReadOnlyDictionary<string, bool> DefaultFlags =
        new SortedDictionary<string, bool>(StringComparer.Ordinal)
        {
            ["journal.core"]                    = true,
            ["mystweld.core"]                   = true,
        };

    public async Task<SortedDictionary<string, bool>> GetEffectiveFlagsAsync(string entitlementId)
    {
        if (settings.Sandbox)
        {
            return new SortedDictionary<string, bool>(
                DefaultFlags.ToDictionary(kv => kv.Key, kv => !kv.Key.StartsWith("admin")),
                StringComparer.Ordinal);
        }

        var eid = NormalizeEntitlementId(entitlementId);
        var flags = new SortedDictionary<string, bool>(
            DefaultFlags.ToDictionary(kv => kv.Key, kv => kv.Value),
            StringComparer.Ordinal);

        await using var conn = new MySqlConnection(settings.MariaDb.FlagsConnectionString);
        var rows = await conn.QueryAsync<(string FlagKey, int Value)>(
            "SELECT flag_key, value FROM overrides WHERE entitlement_id = @eid",
            new { eid });

        foreach (var (key, value) in rows)
        {
            if (flags.ContainsKey(key))
            {
                flags[key] = value != 0;
            }
        }

        return flags;
    }

    public static string EtagFor(SortedDictionary<string, bool> flags)
    {
        // Matches Python: json.dumps(flags, sort_keys=True, separators=(",",":"))
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
