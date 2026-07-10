using Users.Client;
using Users.Config;
using Tangle.AspNetCore.Infrastructure;

namespace Users.Infrastructure;

public static class ChatClientServiceCollectionExtensions
{
    public static IServiceCollection AddTangleChatClient(this IServiceCollection services, IConfiguration configuration) =>
        services.AddTangleInternalClient<HttpChatClient, IChatClient, ChatClientOptions>(
            configuration,
            ChatClientOptions.SectionName,
            "ChatClient:BaseUrl is not configured. Point it at the chat-service base URL (e.g. http://chat:8080 in Compose).",
            client => client.Timeout = UsersHttpClientDefaults.OutboundTimeout);
}
