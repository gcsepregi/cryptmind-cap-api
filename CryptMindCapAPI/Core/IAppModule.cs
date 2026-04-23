namespace CryptMindCapAPI.Core;

public interface IAppModule
{
    string Slug { get; }
    void RegisterServices(IServiceCollection services, IConfiguration configuration);
    void MapEndpoints(RouteGroupBuilder routes);
}
