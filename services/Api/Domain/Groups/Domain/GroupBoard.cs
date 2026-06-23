using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Groups.Domain
{
    [Index(nameof(GroupId), nameof(Name), IsUnique = true)]
    public class GroupBoard
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; private set; }

        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

        [ForeignKey(nameof(Group))]
        public long GroupId { get; private set; }

        public Group? Group { get; private set; }

        public string Name { get; private set; }

        public string? Description { get; private set; }

        public BoardVisibility Visibility { get; private set; }

        public BoardWriteability Writeability { get; private set; }

        private GroupBoard() { }

        public GroupBoard(
            long groupId,
            string name,
            BoardVisibility visibility,
            string? description = null,
            BoardWriteability writeability = BoardWriteability.MembersOnly)
        {
            GroupId = groupId;
            Name = name;
            Visibility = visibility;
            Description = description;
            Writeability = writeability;
        }

        public void Update(
            string name,
            BoardVisibility visibility,
            BoardWriteability writeability,
            string? description)
        {
            Name = name;
            Visibility = visibility;
            Writeability = writeability;
            Description = description;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
