using Media.Client;
using Media.Config;
using Tangle.AspNetCore.Infrastructure;

namespace Media.Infrastructure;

public static class ChatAccessServiceCollectionExtensions
{
    public static IServiceCollection AddTangleChatAccess(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddTangleInternalClient<HttpChatAccessClient, IChatAccessClient, ChatClientOptions>(
            configuration,
            ChatClientOptions.SectionName,
            "ChatClient:BaseUrl is not configured. Point it at the chat-service base URL (e.g. http://chat:8080 in Compose).");
}
