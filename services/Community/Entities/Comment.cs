namespace Community.Entities;

public class Comment
{
    public long Id { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    public string Content { get; private set; } = string.Empty;

    public long? UserId { get; private set; }
    public long? DeletedUserId { get; private set; }
    public long? PostId { get; private set; }
    public long? DeletedPostId { get; private set; }
    public long? ParentId { get; private set; }
    public long? DeletedParentId { get; private set; }

    public Post? Post { get; private set; }
    public Comment? Parent { get; private set; }

    public long AuthorUserId => UserId ?? DeletedUserId!.Value;
    public long LogicalPostId => PostId ?? DeletedPostId!.Value;

    private Comment() { }

    public Comment(string content, long userId, long postId, long? parentId = null)
    {
        Content = content;
        UserId = userId;
        PostId = postId;
        ParentId = parentId;
    }

    public void UpdateContent(string content)
    {
        Content = content;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DetachAuthor(long userId)
    {
        DeletedUserId = userId;
        UserId = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DetachPost(long postId)
    {
        DeletedPostId = postId;
        PostId = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DetachParent(long parentId)
    {
        DeletedParentId = parentId;
        ParentId = null;
        UpdatedAt = DateTime.UtcNow;
    }
}
