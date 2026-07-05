namespace Chat.Tests.Infrastructure;

internal static class ChatTestAuthHelpers
{
    internal static string BuildNickname(string testMethodName, long index) =>
        $"{testMethodName}User{index}";
}
