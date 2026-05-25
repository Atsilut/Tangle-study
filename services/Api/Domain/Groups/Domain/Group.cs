using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Domain.Groups.Domain
{
    public class Group
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; private set; }

        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

        public string Name { get; private set; }
        public string Description { get; private set; }
        public GroupVisibility Visibility { get; private set; }

        public ICollection<GroupMember> Members { get; private set; } = new List<GroupMember>();

        private Group() { }

        public Group(string name, string description, GroupVisibility visibility)
        {
            Name = name;
            Description = description;
            Visibility = visibility;
        }

        public void UpdateDetails(string name, string description, GroupVisibility visibility)
        {
            Name = name;
            Description = description;
            Visibility = visibility;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
