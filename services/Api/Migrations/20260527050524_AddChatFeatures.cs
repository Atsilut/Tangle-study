using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChatFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatRooms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PlatformGroupId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.ForeignKey(
                        name: "FK_ChatRooms_Groups_PlatformGroupId",
                        column: x => x.PlatformGroupId,
                        principalTable: "Groups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChatRooms_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatRooms_Users_UserHighId",
                        column: x => x.UserHighId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatRooms_Users_UserLowId",
                        column: x => x.UserLowId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChatRoomId = table.Column<long>(type: "bigint", nullable: false),
                    SenderUserId = table.Column<long>(type: "bigint", nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_ChatRooms_ChatRoomId",
                        column: x => x.ChatRoomId,
                        principalTable: "ChatRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatMessages_Users_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatRoomParticipants",
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
                        principalTable: "ChatRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatRoomParticipants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ChatRoomId",
                table: "ChatMessages",
                column: "ChatRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SenderUserId",
                table: "ChatMessages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRoomParticipants_ChatRoomId_UserId",
                table: "ChatRoomParticipants",
                columns: new[] { "ChatRoomId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatRoomParticipants_UserId",
                table: "ChatRoomParticipants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_CreatedByUserId",
                table: "ChatRooms",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_PlatformGroupId",
                table: "ChatRooms",
                column: "PlatformGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_UserHighId",
                table: "ChatRooms",
                column: "UserHighId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_UserLowId_UserHighId",
                table: "ChatRooms",
                columns: new[] { "UserLowId", "UserHighId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "ChatRoomParticipants");

            migrationBuilder.DropTable(
                name: "ChatRooms");
        }
    }
}
