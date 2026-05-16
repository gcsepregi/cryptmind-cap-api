using CryptMindCapAPI.Apps.Mythos.Models;
using CryptMindCapAPI.Core.Data;

namespace CryptMindCapAPI.Apps.Mythos.Services;

public sealed class ApplicationService(CouchDbClient couchDb)
{
    public async Task CreateApplicationAsync(CreateApplicationRQ? request)
    {
        if (request?.name is null) return;
        await couchDb.EnsureDbAsync(request.name);
        await couchDb.SetSecurityAsync(request.name);
    }
}
