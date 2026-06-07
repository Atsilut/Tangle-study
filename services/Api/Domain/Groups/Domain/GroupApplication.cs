using Api.Domain.Users.Domain;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Api.Domain.Groups.Domain
{
    [Index(nameof(GroupId), nameof(ApplicantId), IsUnique = true)]
    public class GroupApplication
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [SuppressMessage("Roslynator", "RCS1170", Justification = "Store-generated key; EF requires a writable accessor.")]
        public long Id { get; private set; }

        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

        [ForeignKey(nameof(Group))]
        public long GroupId { get; }
        public Group? Group { get; }

        [ForeignKey(nameof(Applicant))]
        public long ApplicantId { get; }
        public User? Applicant { get; }

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
