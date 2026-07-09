namespace Tangle.TestSupport.Auth;

public static class TestUserIdentity
{
    public static string BuildNickname(string testMethodName, long index = 1) =>
        $"{testMethodName}User{index}";
}
