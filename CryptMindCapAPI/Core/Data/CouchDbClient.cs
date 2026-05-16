using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CryptMindCapAPI.Core.Data;

public sealed class CouchDbClient(HttpClient http)
{
    public async Task EnsureDbAsync(string dbName)
    {
        var check = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, dbName));
        if (check.StatusCode == HttpStatusCode.NotFound)
        {
            (await http.PutAsync(dbName, null)).EnsureSuccessStatusCode();
        }
    }

    public async Task SetSecurityAsync(string dbName)
    {
        var security = new
        {
            admins  = new { names = Array.Empty<string>(), roles = new[] { "_admin" } },
            members = new { names = Array.Empty<string>(), roles = Array.Empty<string>() },
        };
        var content = new StringContent(JsonSerializer.Serialize(security), Encoding.UTF8, "application/json");
        (await http.PutAsync($"{dbName}/_security", content)).EnsureSuccessStatusCode();
    }
}
