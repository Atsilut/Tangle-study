namespace Users.Infrastructure;

public sealed record UserNicknameChangedEvent(
    long UserId,
    string? Nickname,
    bool IsDeleted,
    DateTimeOffset OccurredAt,
    int SchemaVersion = 1);
