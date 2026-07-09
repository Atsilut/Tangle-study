using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi;
using Prometheus;
using Swashbuckle.AspNetCore.Annotations;
using Tangle.AspNetCore.Config;
using Tangle.AspNetCore.Exceptions;
using Tangle.AspNetCore.OpenApi;
using Tangle.AspNetCore.Security;
using Tangle.AspNetCore.Telemetry;

namespace Tangle.AspNetCore.Hosting;

public static class TangleWebApplicationBuilderExtensions
{
    public static WebApplicationBuilder AddTangleServiceDefaults(
        this WebApplicationBuilder builder,
        string swaggerTitle)
    {
        builder.AddTangleAzureMonitor();

        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.EnableAnnotations();
            options.SchemaFilter<SwaggerDefaultValueSchemaFilter>();
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = "v1",
                Title = swaggerTitle,
            });
        });

        builder.Services.Configure<MetricsOptions>(builder.Configuration.GetSection(MetricsOptions.SectionName));
        builder.Services.Configure<InternalAccessOptions>(builder.Configuration.GetSection(InternalAccessOptions.SectionName));
        builder.Services.Configure<GatewayIdentityOptions>(builder.Configuration.GetSection(GatewayIdentityOptions.SectionName));
        builder.Services.AddScoped<InternalAccessAuthorizationFilter>();

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = GatewayIdentityAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = GatewayIdentityAuthenticationHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, GatewayIdentityAuthenticationHandler>(
                GatewayIdentityAuthenticationHandler.SchemeName,
                _ => { });

        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<Auth.CurrentUserAccessor>();

        return builder;
    }

    public static WebApplication UseTangleServicePipeline(this WebApplication app, string swaggerTitle)
    {
        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", $"{swaggerTitle} v1");
                options.RoutePrefix = "api";
            });
        }

        app.UseRouting();
        app.UseHttpMetrics();
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<MetricsScrapeAuthMiddleware>();

        return app;
    }

    public static void MapTangleServiceEndpoints(this WebApplication app)
    {
        app.MapControllers();
        app.MapHealthChecks("/health");
        app.MapMetrics();
    }
}
