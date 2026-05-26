using Api.Domain.Users.Domain;
using Api.Domain.Posts.Domain;
using Microsoft.EntityFrameworkCore;
using Api.Domain.Comments.Domain;
using Api.Domain.Friendships.Domain;
using Api.Domain.UserBlocks.Domain;
using Api.Domain.Groups.Domain;

namespace Api.Global.Db
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Friendship> Friendships { get; set; }
        public DbSet<FriendRequest> FriendRequests { get; set; }
        public DbSet<UserBlock> UserBlocks { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupMember> GroupMembers { get; set; }
        public DbSet<GroupInvitation> GroupInvitations { get; set; }
        public DbSet<GroupApplication> GroupApplications { get; set; }
        public DbSet<GroupBlacklist> GroupBlacklists { get; set; }
        public DbSet<GroupBoard> GroupBoards { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Post>()
                .HasOne(p => p.User)
                .WithMany(u => u.Posts)
                .HasForeignKey(p => p.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Post>()
                .HasOne(p => p.Group)
                .WithMany()
                .HasForeignKey(p => p.GroupId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Post>()
                .HasOne(p => p.GroupBoard)
                .WithMany()
                .HasForeignKey(p => p.GroupBoardId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(c => c.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Post)
                .WithMany(p => p.Comments)
                .HasForeignKey(c => c.PostId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Parent)
                .WithMany()
                .HasForeignKey(c => c.ParentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Friendship>()
                .HasOne(f => f.UserLow)
                .WithMany()
                .HasForeignKey(f => f.UserLowId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Friendship>()
                .HasOne(f => f.UserHigh)
                .WithMany()
                .HasForeignKey(f => f.UserHighId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Friendship>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_Friendships_UserLowLtUserHigh",
                    "\"UserLowId\" < \"UserHighId\""));

            modelBuilder.Entity<FriendRequest>()
                .HasOne(r => r.Requester)
                .WithMany()
                .HasForeignKey(r => r.RequesterId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FriendRequest>()
                .HasOne(r => r.Addressee)
                .WithMany()
                .HasForeignKey(r => r.AddresseeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserBlock>()
                .HasOne(b => b.Blocker)
                .WithMany()
                .HasForeignKey(b => b.BlockerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserBlock>()
                .HasOne(b => b.BlockedUser)
                .WithMany()
                .HasForeignKey(b => b.BlockedUserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GroupMember>()
                .HasOne(m => m.Group)
                .WithMany(g => g.Members)
                .HasForeignKey(m => m.GroupId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<GroupMember>()
                .HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<GroupInvitation>()
                .HasOne(i => i.Group)
                .WithMany()
                .HasForeignKey(i => i.GroupId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<GroupInvitation>()
                .HasOne(i => i.Inviter)
                .WithMany()
                .HasForeignKey(i => i.InviterId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GroupInvitation>()
                .HasOne(i => i.Invitee)
                .WithMany()
                .HasForeignKey(i => i.InviteeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GroupApplication>()
                .HasOne(a => a.Group)
                .WithMany()
                .HasForeignKey(a => a.GroupId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<GroupApplication>()
                .HasOne(a => a.Applicant)
                .WithMany()
                .HasForeignKey(a => a.ApplicantId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GroupBlacklist>()
                .HasOne(b => b.Group)
                .WithMany()
                .HasForeignKey(b => b.GroupId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<GroupBlacklist>()
                .HasOne(b => b.User)
                .WithMany()
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GroupBoard>()
                .HasOne(b => b.Group)
                .WithMany()
                .HasForeignKey(b => b.GroupId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
