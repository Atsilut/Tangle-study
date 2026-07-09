using Gateway.Config;
using Gateway.Middleware;
using Gateway.Security;
using Microsoft.Extensions.Options;
using Prometheus;
using Tangle.AspNetCore.Security;
using Tangle.AspNetCore.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddYamlFile("security.yml", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<GatewayOptions>(builder.Configuration.GetSection(GatewayOptions.SectionName));
builder.Services.Configure<Tangle.AspNetCore.Config.MetricsOptions>(
    builder.Configuration.GetSection(Tangle.AspNetCore.Config.MetricsOptions.SectionName));
builder.Services.AddSingleton<JwtBearerValidator>();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

var jwtOptions = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
JwtStartupValidator.Validate(app.Environment, jwtOptions.Secret);

app.UseRouting();
app.UseHttpMetrics();
app.UseWebSockets();
app.UseMiddleware<GatewayAuthMiddleware>();
app.UseMiddleware<MetricsScrapeAuthMiddleware>();

app.MapGet("/health", () => Results.Ok());
app.MapMetrics();
app.MapReverseProxy();

app.Run();

public partial class Program { }
