using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Users.Domain
{
    [Index(nameof(Email), IsUnique = true)]
    [Index(nameof(Nickname), IsUnique = true)]
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; private set; }
        public string Email { get; private set; }
        public string Password { get; private set; }
        public string Nickname { get; private set; }
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
        public FriendsListVisibility FriendsListVisibility { get; private set; } = FriendsListVisibility.Private;

        private User() { }

        public User(string email, string password, string nickname)
        {
            Email = email;
            Password = password;
            Nickname = nickname;
        }

        public void UpdateNickname(string nickname)
        {
            Nickname = nickname;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateFriendsListVisibility(FriendsListVisibility visibility)
        {
            FriendsListVisibility = visibility;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
