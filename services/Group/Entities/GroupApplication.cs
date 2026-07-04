using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Group.Entities
{
    [Index(nameof(GroupId), nameof(ApplicantId), IsUnique = true)]
    public class GroupApplication
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; private set; }

        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

        public long GroupId { get; private set; }
        public Group? Group { get; private set; }

        public long ApplicantId { get; private set; }

        public bool IsPending { get; private set; } = true;

        private GroupApplication() { }

        public GroupApplication(long groupId, long applicantId)
        {
            GroupId = groupId;
            ApplicantId = applicantId;
        }

        public void Ignore()
        {
            IsPending = false;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
