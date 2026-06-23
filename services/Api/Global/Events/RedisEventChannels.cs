namespace Api.Global.Events;

public static class RedisEventChannels
{
    public const string ChatMessageCreated = "tangle:events:chat.message.created";
    public const string UserNicknameChanged = "tangle:events:user.nickname.changed";
}
