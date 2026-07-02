using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class DropMediaAssetForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaAssets_ChatMessages_ChatMessageId",
                table: "MediaAssets");

            migrationBuilder.DropForeignKey(
                name: "FK_MediaAssets_Comments_CommentId",
                table: "MediaAssets");

            migrationBuilder.DropForeignKey(
                name: "FK_MediaAssets_Posts_PostId",
                table: "MediaAssets");

            migrationBuilder.DropForeignKey(
                name: "FK_MediaAssets_Users_UploaderId",
                table: "MediaAssets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_MediaAssets_ChatMessages_ChatMessageId",
                table: "MediaAssets",
                column: "ChatMessageId",
                principalTable: "ChatMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MediaAssets_Comments_CommentId",
                table: "MediaAssets",
                column: "CommentId",
                principalTable: "Comments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MediaAssets_Posts_PostId",
                table: "MediaAssets",
                column: "PostId",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MediaAssets_Users_UploaderId",
                table: "MediaAssets",
                column: "UploaderId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
