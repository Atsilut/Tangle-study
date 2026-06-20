using Api.Domain.Users.Domain;
using Api.Domain.Posts.Domain;
using Microsoft.EntityFrameworkCore;
using Api.Domain.Comments.Domain;
using Api.Domain.Friendships.Domain;
using Api.Domain.UserBlocks.Domain;
using Api.Domain.Groups.Domain;
using Api.Domain.Chat.Domain;
using Api.Domain.Media.Domain;
using Api.Domain.Location.Domain;

namespace Api.Global.Db
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<Post> Posts => Set<Post>();
        public DbSet<Comment> Comments => Set<Comment>();
        public DbSet<Friendship> Friendships => Set<Friendship>();
        public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();
        public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
        public DbSet<Group> Groups => Set<Group>();
        public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
        public DbSet<GroupInvitation> GroupInvitations => Set<GroupInvitation>();
        public DbSet<GroupApplication> GroupApplications => Set<GroupApplication>();
        public DbSet<GroupBlacklist> GroupBlacklists => Set<GroupBlacklist>();
        public DbSet<GroupBoard> GroupBoards => Set<GroupBoard>();
        public DbSet<ChatRoom> ChatRooms => Set<ChatRoom>();
        public DbSet<ChatRoomParticipant> ChatRoomParticipants => Set<ChatRoomParticipant>();
        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
        public DbSet<ChatMessageReceipt> ChatMessageReceipts => Set<ChatMessageReceipt>();
        public DbSet<ChatMessageEdit> ChatMessageEdits => Set<ChatMessageEdit>();
        public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
        public DbSet<MapPin> MapPins => Set<MapPin>();
        public DbSet<LocationSession> LocationSessions => Set<LocationSession>();

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

            modelBuilder.Entity<ChatRoom>()
                .HasOne(r => r.PlatformGroup)
                .WithMany()
                .HasForeignKey(r => r.PlatformGroupId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ChatRoom>()
                .HasOne(r => r.CreatedByUser)
                .WithMany()
                .HasForeignKey(r => r.CreatedByUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ChatRoom>()
                .HasOne(r => r.UserLow)
                .WithMany()
                .HasForeignKey(r => r.UserLowId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ChatRoom>()
                .HasOne(r => r.UserHigh)
                .WithMany()
                .HasForeignKey(r => r.UserHighId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ChatRoom>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_ChatRooms_DirectUserLowLtUserHigh",
                    "\"Kind\" <> 0 OR (\"UserLowId\" IS NOT NULL AND \"UserHighId\" IS NOT NULL AND \"UserLowId\" < \"UserHighId\")"));

            modelBuilder.Entity<ChatRoom>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_ChatRooms_PlatformGroupIdByKind",
                    "(\"Kind\" = 2 AND \"PlatformGroupId\" IS NOT NULL) OR (\"Kind\" <> 2 AND \"PlatformGroupId\" IS NULL)"));

            modelBuilder.Entity<ChatRoom>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_ChatRooms_DirectPairOnlyForDirect",
                    "\"Kind\" <> 0 OR (\"UserLowId\" IS NOT NULL AND \"UserHighId\" IS NOT NULL)"));

            modelBuilder.Entity<ChatRoom>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_ChatRooms_NoDirectPairForNonDirect",
                    "\"Kind\" = 0 OR (\"UserLowId\" IS NULL AND \"UserHighId\" IS NULL)"));

            modelBuilder.Entity<ChatRoomParticipant>()
                .HasOne(p => p.ChatRoom)
                .WithMany(r => r.Participants)
                .HasForeignKey(p => p.ChatRoomId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatRoomParticipant>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.ChatRoom)
                .WithMany()
                .HasForeignKey(m => m.ChatRoomId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // Latest-message-per-room list queries: filter by ChatRoomId, order by Id DESC.
            modelBuilder.Entity<ChatMessage>()
                .HasIndex(m => new { m.ChatRoomId, m.Id })
                .IsDescending(false, true);

            modelBuilder.Entity<ChatMessageReceipt>()
                .HasOne(r => r.ChatMessage)
                .WithMany()
                .HasForeignKey(r => r.ChatMessageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMessageReceipt>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ChatMessageEdit>()
                .HasOne(e => e.ChatMessage)
                .WithMany()
                .HasForeignKey(e => e.ChatMessageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MediaAsset>()
                .HasOne(m => m.Uploader)
                .WithMany()
                .HasForeignKey(m => m.UploaderId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<MediaAsset>()
                .HasOne(m => m.Post)
                .WithMany()
                .HasForeignKey(m => m.PostId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MediaAsset>()
                .HasOne(m => m.Comment)
                .WithMany()
                .HasForeignKey(m => m.CommentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MediaAsset>()
                .HasOne(m => m.ChatMessage)
                .WithMany()
                .HasForeignKey(m => m.ChatMessageId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MediaAsset>()
                .HasIndex(m => m.UploaderId);

            modelBuilder.Entity<MapPin>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<MapPin>()
                .HasOne(p => p.Post)
                .WithMany()
                .HasForeignKey(p => p.PostId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MapPin>()
                .HasIndex(p => new { p.Latitude, p.Longitude });

            modelBuilder.Entity<MapPin>()
                .HasIndex(p => p.UserId);

            modelBuilder.Entity<LocationSession>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<LocationSession>()
                .HasOne(s => s.Group)
                .WithMany()
                .HasForeignKey(s => s.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LocationSession>()
                .HasIndex(s => new { s.GroupId, s.EndedAt });

            modelBuilder.Entity<LocationSession>()
                .HasIndex(s => new { s.UserId, s.GroupId, s.EndedAt });

            modelBuilder.Entity<LocationSession>()
                .HasIndex(s => new { s.UserId, s.GroupId })
                .IsUnique()
                .HasFilter("\"EndedAt\" IS NULL");
        }
    }
}
