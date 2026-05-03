using CryptMindCapAPI.Apps.CryptMind;
using CryptMindCapAPI.Apps.Mystweld;
using CryptMindCapAPI.Core;
using CryptMindCapAPI.Core.Auth;

var builder = WebApplication.CreateBuilder(args);
var settings = AppSettings.From(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "CryptMindCorsPolicy", 
        policy => 
            policy.WithOrigins("http://localhost:4201")
                .AllowAnyHeader()
                .AllowAnyMethod()
            );
});
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton<ZeroKnowledgeAuthService>();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
});

IAppModule[] modules = [new CryptMindModule(), new MystweldModule()];

foreach (var module in modules)
{
    module.RegisterServices(builder.Services, builder.Configuration);
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseCors("CryptMindCorsPolicy");
    app.MapOpenApi();
}

app.MapGet("/healthz", () => Results.Ok(new { ok = true, service = "4shards-api" }))
    .WithTags("health");

foreach (var module in modules)
{
    var group = app.MapGroup($"/{module.Slug}/v1").WithTags(module.Slug);
    group.MapGet("/healthz", () => Results.Ok(new { ok = true, service = "4shards-api", app = module.Slug }));
    module.MapEndpoints(group);
}

app.Run();
