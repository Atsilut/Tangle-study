using Users.Client;
using Users.Config;

namespace Users.Infrastructure;

public static class ChatClientServiceCollectionExtensions
{
    public static IServiceCollection AddTangleChatClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ChatClientOptions>(configuration.GetSection(ChatClientOptions.SectionName));

        var options = configuration.GetSection(ChatClientOptions.SectionName).Get<ChatClientOptions>()
            ?? new ChatClientOptions();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException(
                "ChatClient:BaseUrl is not configured. Point it at the chat-service base URL (e.g. http://chat:8080 in Compose).");
        }

        services.AddHttpClient(nameof(HttpChatClient), client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = UsersHttpClientDefaults.OutboundTimeout;
        });
        services.AddScoped<IChatClient, HttpChatClient>();

        return services;
    }
}
