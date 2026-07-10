using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Chat.Migrations
{
    /// <inheritdoc />
    public partial class InitialChatSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "chat");

            migrationBuilder.CreateTable(
                name: "ChatRooms",
                schema: "chat",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PlatformGroupId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    DeletedCreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    UserLowId = table.Column<long>(type: "bigint", nullable: true),
                    UserHighId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatRooms", x => x.Id);
                    table.CheckConstraint("CK_ChatRooms_DirectPairOnlyForDirect", "\"Kind\" <> 0 OR (\"UserLowId\" IS NOT NULL AND \"UserHighId\" IS NOT NULL)");
                    table.CheckConstraint("CK_ChatRooms_DirectUserLowLtUserHigh", "\"Kind\" <> 0 OR (\"UserLowId\" IS NOT NULL AND \"UserHighId\" IS NOT NULL AND \"UserLowId\" < \"UserHighId\")");
                    table.CheckConstraint("CK_ChatRooms_NoDirectPairForNonDirect", "\"Kind\" = 0 OR (\"UserLowId\" IS NULL AND \"UserHighId\" IS NULL)");
                    table.CheckConstraint("CK_ChatRooms_PlatformGroupIdByKind", "(\"Kind\" = 2 AND \"PlatformGroupId\" IS NOT NULL) OR (\"Kind\" <> 2 AND \"PlatformGroupId\" IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                schema: "chat",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ChatRoomId = table.Column<long>(type: "bigint", nullable: false),
                    SenderUserId = table.Column<long>(type: "bigint", nullable: true),
                    DeletedSenderUserId = table.Column<long>(type: "bigint", nullable: true),
                    Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_ChatRooms_ChatRoomId",
                        column: x => x.ChatRoomId,
                        principalSchema: "chat",
                        principalTable: "ChatRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatRoomParticipants",
                schema: "chat",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChatRoomId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatRoomParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatRoomParticipants_ChatRooms_ChatRoomId",
                        column: x => x.ChatRoomId,
                        principalSchema: "chat",
                        principalTable: "ChatRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessageEdits",
                schema: "chat",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChatMessageId = table.Column<long>(type: "bigint", nullable: false),
                    Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessageEdits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessageEdits_ChatMessages_ChatMessageId",
                        column: x => x.ChatMessageId,
                        principalSchema: "chat",
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessageReceipts",
                schema: "chat",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChatMessageId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    SeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessageReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessageReceipts_ChatMessages_ChatMessageId",
                        column: x => x.ChatMessageId,
                        principalSchema: "chat",
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessageEdits_ChatMessageId",
                schema: "chat",
                table: "ChatMessageEdits",
                column: "ChatMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessageReceipts_ChatMessageId_UserId",
                schema: "chat",
                table: "ChatMessageReceipts",
                columns: new[] { "ChatMessageId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ChatRoomId_Id",
                schema: "chat",
                table: "ChatMessages",
                columns: new[] { "ChatRoomId", "Id" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ChatRoomParticipants_ChatRoomId_UserId",
                schema: "chat",
                table: "ChatRoomParticipants",
                columns: new[] { "ChatRoomId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_PlatformGroupId",
                schema: "chat",
                table: "ChatRooms",
                column: "PlatformGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_UserLowId_UserHighId",
                schema: "chat",
                table: "ChatRooms",
                columns: new[] { "UserLowId", "UserHighId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessageEdits",
                schema: "chat");

            migrationBuilder.DropTable(
                name: "ChatMessageReceipts",
                schema: "chat");

            migrationBuilder.DropTable(
                name: "ChatRoomParticipants",
                schema: "chat");

            migrationBuilder.DropTable(
                name: "ChatMessages",
                schema: "chat");

            migrationBuilder.DropTable(
                name: "ChatRooms",
                schema: "chat");
        }
    }
}
