using Microsoft.AspNetCore.SignalR.Client;

namespace Tangle.TestSupport.Harness;

public static class HarnessHubConnectionFactory
{
    public static HubConnection Build(HttpClient client, string hubPath)
    {
        var hubUrl = new Uri(client.BaseAddress!, hubPath.TrimStart('/'));
        return new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () =>
                {
                    var token = client.DefaultRequestHeaders.Authorization?.Parameter;
                    return Task.FromResult(token);
                };
            })
            .Build();
    }
}
