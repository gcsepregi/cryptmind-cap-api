using CryptMindCapAPI.Core;
using CryptMindCapAPI.Core.Endpoints;
using CryptMindCapAPI.Core.Services;

namespace CryptMindCapAPI.Apps.CryptMind;

public class CryptMindModule : IAppModule
{
    public string Slug => "cryptmind";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<FeaturesService>();
    }

    public void MapEndpoints(RouteGroupBuilder routes)
    {
        routes.MapFeaturesEndpoints();
    }
}
