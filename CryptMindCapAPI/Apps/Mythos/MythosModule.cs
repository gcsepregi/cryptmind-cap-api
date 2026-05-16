using CryptMindCapAPI.Apps.Mythos.Endpoints;
using CryptMindCapAPI.Core;
using CryptMindCapAPI.Core.Endpoints;

namespace CryptMindCapAPI.Apps.Mythos;

public class MythosModule : IAppModule
{
    public string Slug => "mythos";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(RouteGroupBuilder routes)
    {
        routes.MapFeaturesEndpoints();
        routes.MapApplicationEndpoints();
    }
}
