using Api.Domain.Posts.Domain;
using Api.Domain.Users.Domain;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Domain.Comments.Domain
{
    public class Comment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; private set; }
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string Content { get; set; }

        [ForeignKey(nameof(User))]
        public long UserId { get; private set; }
        public User User { get; private set; }

        [ForeignKey(nameof(Post))]
        public long PostId { get; private set; }
        public Post Post { get; private set; }

        [ForeignKey(nameof(Parent))]
        public long? ParentId { get; private set; }
        public Comment? Parent { get; private set; }

        private Comment() { }

        public Comment(string content, long userId, long postId, long? parentId = null)
        {
            Content = content;
            UserId = userId;
            PostId = postId;
            ParentId = parentId;
        }
    }
}
