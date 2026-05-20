using Api.Domain.Comments.Domain;
using Api.Domain.Users.Domain;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Domain.Posts.Domain
{
    public class Post
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; private set; }

        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
        public string Title { get; private set; }
        public string Content { get; private set; }

        [ForeignKey(nameof(User))]
        public long UserId { get; private set; }

        public User User { get; private set; }

        public ICollection<Comment> Comments { get; private set; } = new List<Comment>();

        private Post() { }

        public Post(long userId, string title, string content)
        {
            UserId = userId;
            Title = title;
            Content = content;
        }

        public void Update(string title, string content)
        {
            Title = title;
            Content = content;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
