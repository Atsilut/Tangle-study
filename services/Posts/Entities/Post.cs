namespace Posts.Entities;

public class Post
{
    public long Id { get; private set; }

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;

    public long? UserId { get; private set; }
    public long? DeletedUserId { get; private set; }
    public long? GroupId { get; private set; }
    public long? GroupBoardId { get; private set; }

    public ICollection<Comment> Comments { get; private set; } = [];

    public long AuthorUserId => UserId ?? DeletedUserId!.Value;

    private Post() { }

    public Post(long userId, string title, string content, long? groupId = null, long? groupBoardId = null)
    {
        UserId = userId;
        Title = title;
        Content = content;
        GroupId = groupId;
        GroupBoardId = groupBoardId;
    }

    public void Update(string title, string content)
    {
        Title = title;
        Content = content;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DetachAuthor(long userId)
    {
        DeletedUserId = userId;
        UserId = null;
        UpdatedAt = DateTime.UtcNow;
    }
}
