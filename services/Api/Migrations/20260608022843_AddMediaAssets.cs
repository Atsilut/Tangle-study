using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Users_SenderUserId",
                table: "ChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatRoomParticipants_Users_UserId",
                table: "ChatRoomParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatRooms_Users_CreatedByUserId",
                table: "ChatRooms");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatRooms_Users_UserHighId",
                table: "ChatRooms");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatRooms_Users_UserLowId",
                table: "ChatRooms");

            migrationBuilder.AlterColumn<long>(
                name: "CreatedByUserId",
                table: "ChatRooms",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "DeletedCreatedByUserId",
                table: "ChatRooms",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "SenderUserId",
                table: "ChatMessages",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "DeletedSenderUserId",
                table: "ChatMessages",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MediaAssets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UploaderId = table.Column<long>(type: "bigint", nullable: true),
                    DeletedUploaderId = table.Column<long>(type: "bigint", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    IntendedContext = table.Column<int>(type: "integer", nullable: false),
                    ProcessingStatus = table.Column<int>(type: "integer", nullable: false),
                    MimeType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    OriginalObjectKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ProcessedObjectKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    OriginalSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoredSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PostId = table.Column<long>(type: "bigint", nullable: true),
                    CommentId = table.Column<long>(type: "bigint", nullable: true),
                    ChatMessageId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaAssets_ChatMessages_ChatMessageId",
                        column: x => x.ChatMessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaAssets_Comments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "Comments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaAssets_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaAssets_Users_UploaderId",
                        column: x => x.UploaderId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_ChatMessageId",
                table: "MediaAssets",
                column: "ChatMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_CommentId",
                table: "MediaAssets",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_PostId",
                table: "MediaAssets",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_UploaderId",
                table: "MediaAssets",
                column: "UploaderId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Users_SenderUserId",
                table: "ChatMessages",
                column: "SenderUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRoomParticipants_Users_UserId",
                table: "ChatRoomParticipants",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRooms_Users_CreatedByUserId",
                table: "ChatRooms",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRooms_Users_UserHighId",
                table: "ChatRooms",
                column: "UserHighId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRooms_Users_UserLowId",
                table: "ChatRooms",
                column: "UserLowId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Users_SenderUserId",
                table: "ChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatRoomParticipants_Users_UserId",
                table: "ChatRoomParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatRooms_Users_CreatedByUserId",
                table: "ChatRooms");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatRooms_Users_UserHighId",
                table: "ChatRooms");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatRooms_Users_UserLowId",
                table: "ChatRooms");

            migrationBuilder.DropTable(
                name: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "DeletedCreatedByUserId",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "DeletedSenderUserId",
                table: "ChatMessages");

            migrationBuilder.AlterColumn<long>(
                name: "CreatedByUserId",
                table: "ChatRooms",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "SenderUserId",
                table: "ChatMessages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Users_SenderUserId",
                table: "ChatMessages",
                column: "SenderUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRoomParticipants_Users_UserId",
                table: "ChatRoomParticipants",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRooms_Users_CreatedByUserId",
                table: "ChatRooms",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRooms_Users_UserHighId",
                table: "ChatRooms",
                column: "UserHighId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRooms_Users_UserLowId",
                table: "ChatRooms",
                column: "UserLowId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
