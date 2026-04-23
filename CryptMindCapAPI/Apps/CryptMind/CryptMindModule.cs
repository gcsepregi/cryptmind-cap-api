using CryptMindCapAPI.Core;

namespace CryptMindCapAPI.Apps.CryptMind;

public class CryptMindModule : IAppModule
{
    public string Slug => "cryptmind";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(RouteGroupBuilder routes)
    {
    }
}
