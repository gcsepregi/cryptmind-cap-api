using CryptMindCapAPI.Core.Auth;
using CryptMindCapAPI.Core.Services;

namespace CryptMindCapAPI.Core.Endpoints;

public static class FeaturesEndpoints
{
    public static void MapFeaturesEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/features", async (HttpContext ctx, FeaturesService features) =>
        {
            var entitlementId = ctx.ZkUserSpaceId();
            var flags = await features.GetEffectiveFlagsAsync(entitlementId);
            var etag = FeaturesService.EtagFor(flags);
            return Results.Ok(new FlagsResponse(flags, etag));
        })
        .RequireZkAuth();
    }
}
