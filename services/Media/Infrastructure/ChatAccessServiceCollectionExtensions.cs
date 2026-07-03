using Media.Client;
using Media.Config;

namespace Media.Infrastructure;

public static class ChatAccessServiceCollectionExtensions
{
    public static IServiceCollection AddTangleChatAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ChatClientOptions>(configuration.GetSection(ChatClientOptions.SectionName));
        var options = configuration.GetSection(ChatClientOptions.SectionName).Get<ChatClientOptions>()
            ?? new ChatClientOptions();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException(
                "ChatClient:BaseUrl is not configured. Point it at the chat-service base URL (e.g. http://chat:8080 in Compose).");
        }

        services.AddHttpClient(nameof(HttpChatAccessClient), client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        });
        services.AddScoped<IChatAccessClient, HttpChatAccessClient>();

        return services;
    }
}
