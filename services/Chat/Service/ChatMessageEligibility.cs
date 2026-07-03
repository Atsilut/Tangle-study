using Chat.Config;
using Chat.Entities;

namespace Chat.Service;

internal static class ChatMessageEligibility
{
    public static bool CanEdit(
        ChatMessage message,
        long viewerUserId,
        ChatMessagePolicyOptions policy,
        DateTime utcNow)
    {
        if (message.IsDeleted) return false;
        if (message.SenderUserId != viewerUserId) return false;
        return utcNow - message.SentAt <= TimeSpan.FromMinutes(policy.EditWindowMinutes);
    }

    public static bool CanDelete(
        ChatMessage message,
        long viewerUserId,
        bool seenByOtherParticipant,
        ChatMessagePolicyOptions policy,
        DateTime utcNow)
    {
        if (message.IsDeleted) return false;
        if (message.SenderUserId != viewerUserId) return false;
        if (seenByOtherParticipant) return false;
        return utcNow - message.SentAt <= TimeSpan.FromMinutes(policy.DeleteWindowMinutes);
    }
}
