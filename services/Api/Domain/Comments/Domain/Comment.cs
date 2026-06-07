using Api.Domain.Posts.Domain;
using Api.Domain.Users.Domain;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Api.Domain.Comments.Domain
{
    public class Comment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [SuppressMessage("Roslynator", "RCS1170", Justification = "Store-generated key; EF requires a writable accessor.")]
        public long Id { get; private set; }
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
        public string Content { get; private set; }

        [ForeignKey(nameof(User))]
        public long? UserId { get; private set; }

        public long? DeletedUserId { get; private set; }

        public User? User { get; }

        [ForeignKey(nameof(Post))]
        public long? PostId { get; private set; }

        public long? DeletedPostId { get; private set; }

        public Post? Post { get; }

        [ForeignKey(nameof(Parent))]
        public long? ParentId { get; private set; }

        public long? DeletedParentId { get; private set; }

        public Comment? Parent { get; }

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
}
