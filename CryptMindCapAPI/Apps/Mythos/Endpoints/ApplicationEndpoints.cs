using CryptMindCapAPI.Apps.Mythos.Models;
using CryptMindCapAPI.Apps.Mythos.Services;
using CryptMindCapAPI.Core.Auth;

namespace CryptMindCapAPI.Apps.Mythos.Endpoints;

public static class ApplicationEndpoints
{
    public static void MapApplicationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/applications", async (HttpContext ctx, ApplicationService service) =>
            {
                await service.CreateApplicationAsync(await ctx.Request.ReadFromJsonAsync<CreateApplicationRQ>());
                return Results.Created();
            })
            .RequireZkAuth();
    }
}