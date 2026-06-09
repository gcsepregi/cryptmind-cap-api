using System.Net.Http.Headers;
using System.Text;
using CryptMindCapAPI.Apps.CryptMind;
using CryptMindCapAPI.Apps.Mystweld;
using CryptMindCapAPI.Apps.Mythos;
using CryptMindCapAPI.Apps.Mythos.Services;
using CryptMindCapAPI.Core;
using CryptMindCapAPI.Core.Auth;
using CryptMindCapAPI.Core.Data;
using CryptMindCapAPI.Core.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var settings = AppSettings.From(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "CryptMindCorsPolicy", 
        policy => 
            policy.WithOrigins("http://localhost:4201", "http://localhost:4202")
                .AllowAnyHeader()
                .AllowAnyMethod()
            );
});
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton<ZeroKnowledgeAuthService>();
builder.Services.AddDbContext<FlagsDbContext>(options =>
    options.UseMySql(settings.MariaDb.FlagsConnectionString,
        ServerVersion.AutoDetect(settings.MariaDb.FlagsConnectionString)));
builder.Services.AddScoped<FeaturesService>();
builder.Services.AddHttpClient<CouchDbClient>((sp, client) =>
{
    var s = sp.GetRequiredService<AppSettings>();
    client.BaseAddress = new Uri(s.CouchDb.Url.TrimEnd('/') + "/");
    var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{s.CouchDb.AdminUser}:{s.CouchDb.AdminPassword}"));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
});
builder.Services.AddScoped<ApplicationService>();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
});

IAppModule[] modules = [new CryptMindModule(), new MystweldModule(), new MythosModule()];

foreach (var module in modules)
{
    module.RegisterServices(builder.Services, builder.Configuration);
}

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FlagsDbContext>();
    await db.Database.MigrateAsync();
}

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
