using Gateway.Config;
using Gateway.Middleware;
using Gateway.Security;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddYamlFile("security.yml", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<GatewayOptions>(builder.Configuration.GetSection(GatewayOptions.SectionName));
builder.Services.AddSingleton<JwtBearerValidator>();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

var jwtOptions = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
JwtStartupValidator.Validate(app.Environment, jwtOptions);

app.UseRouting();
app.UseWebSockets();
app.UseMiddleware<GatewayAuthMiddleware>();

app.MapGet("/health", () => Results.Ok());
app.MapReverseProxy();

app.Run();
