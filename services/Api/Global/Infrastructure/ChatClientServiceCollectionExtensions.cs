using Api.Client;
using Api.Global.Config;

namespace Api.Global.Infrastructure;

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
        });
        services.AddScoped<IChatClient, HttpChatClient>();

        return services;
    }
}
