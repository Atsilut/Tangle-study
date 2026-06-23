using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class CascadeDeleteSocialGraphOnUserDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_Users_AddresseeId",
                table: "FriendRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_Users_RequesterId",
                table: "FriendRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Friendships_Users_UserHighId",
                table: "Friendships");

            migrationBuilder.DropForeignKey(
                name: "FK_Friendships_Users_UserLowId",
                table: "Friendships");

            migrationBuilder.DropForeignKey(
                name: "FK_UserBlocks_Users_BlockedUserId",
                table: "UserBlocks");

            migrationBuilder.DropForeignKey(
                name: "FK_UserBlocks_Users_BlockerId",
                table: "UserBlocks");

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_Users_AddresseeId",
                table: "FriendRequests",
                column: "AddresseeId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_Users_RequesterId",
                table: "FriendRequests",
                column: "RequesterId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Friendships_Users_UserHighId",
                table: "Friendships",
                column: "UserHighId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Friendships_Users_UserLowId",
                table: "Friendships",
                column: "UserLowId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserBlocks_Users_BlockedUserId",
                table: "UserBlocks",
                column: "BlockedUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserBlocks_Users_BlockerId",
                table: "UserBlocks",
                column: "BlockerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_Users_AddresseeId",
                table: "FriendRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_Users_RequesterId",
                table: "FriendRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Friendships_Users_UserHighId",
                table: "Friendships");

            migrationBuilder.DropForeignKey(
                name: "FK_Friendships_Users_UserLowId",
                table: "Friendships");

            migrationBuilder.DropForeignKey(
                name: "FK_UserBlocks_Users_BlockedUserId",
                table: "UserBlocks");

            migrationBuilder.DropForeignKey(
                name: "FK_UserBlocks_Users_BlockerId",
                table: "UserBlocks");

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_Users_AddresseeId",
                table: "FriendRequests",
                column: "AddresseeId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_Users_RequesterId",
                table: "FriendRequests",
                column: "RequesterId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Friendships_Users_UserHighId",
                table: "Friendships",
                column: "UserHighId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Friendships_Users_UserLowId",
                table: "Friendships",
                column: "UserLowId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserBlocks_Users_BlockedUserId",
                table: "UserBlocks",
                column: "BlockedUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserBlocks_Users_BlockerId",
                table: "UserBlocks",
                column: "BlockerId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
