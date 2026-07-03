namespace Chat.Config;

public class ChatMessagePolicyOptions
{
    public const string SectionName = "Chat";

    /// <summary>Minutes after send during which the sender may edit.</summary>
    public int EditWindowMinutes { get; set; } = 15;

    /// <summary>Minutes after send during which the sender may delete.</summary>
    public int DeleteWindowMinutes { get; set; } = 60;
}
