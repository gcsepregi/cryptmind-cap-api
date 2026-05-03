using CryptMindCapAPI.Apps.Mystweld.Endpoints;
using CryptMindCapAPI.Core;

namespace CryptMindCapAPI.Apps.Mystweld;

public class MystweldModule : IAppModule
{
    public string Slug => "mystweld";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(RouteGroupBuilder routes)
    {
        routes.MapFeaturesEndpoints();
    }
}
