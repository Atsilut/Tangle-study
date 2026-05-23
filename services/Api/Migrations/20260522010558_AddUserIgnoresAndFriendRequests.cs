using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIgnoresAndFriendRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Friendships_Users_AddresseeId",
                table: "Friendships");

            migrationBuilder.DropForeignKey(
                name: "FK_Friendships_Users_RequesterId",
                table: "Friendships");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Friendships");

            migrationBuilder.RenameColumn(
                name: "RequesterId",
                table: "Friendships",
                newName: "UserLowId");

            migrationBuilder.RenameColumn(
                name: "AddresseeId",
                table: "Friendships",
                newName: "UserHighId");

            migrationBuilder.RenameIndex(
                name: "IX_Friendships_RequesterId_AddresseeId",
                table: "Friendships",
                newName: "IX_Friendships_UserLowId_UserHighId");

            migrationBuilder.RenameIndex(
                name: "IX_Friendships_AddresseeId",
                table: "Friendships",
                newName: "IX_Friendships_UserHighId");

            // RequesterId/AddresseeId are directional; UserLowId/UserHighId require ordering.
            migrationBuilder.DropIndex(
                name: "IX_Friendships_UserLowId_UserHighId",
                table: "Friendships");

            migrationBuilder.Sql(
                """
                UPDATE "Friendships"
                SET "UserLowId" = LEAST("UserLowId", "UserHighId"),
                    "UserHighId" = GREATEST("UserLowId", "UserHighId")
                WHERE "UserLowId" > "UserHighId";
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM "Friendships"
                WHERE "UserLowId" = "UserHighId";
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM "Friendships" a
                USING "Friendships" b
                WHERE a."Id" > b."Id"
                  AND a."UserLowId" = b."UserLowId"
                  AND a."UserHighId" = b."UserHighId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_UserLowId_UserHighId",
                table: "Friendships",
                columns: new[] { "UserLowId", "UserHighId" },
                unique: true);

            migrationBuilder.CreateTable(
                name: "FriendRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequesterId = table.Column<long>(type: "bigint", nullable: false),
                    AddresseeId = table.Column<long>(type: "bigint", nullable: false),
                    IsPending = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FriendRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FriendRequests_Users_AddresseeId",
                        column: x => x.AddresseeId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FriendRequests_Users_RequesterId",
                        column: x => x.RequesterId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserBlocks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BlockerId = table.Column<long>(type: "bigint", nullable: false),
                    BlockedUserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserBlocks_Users_BlockedUserId",
                        column: x => x.BlockedUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserBlocks_Users_BlockerId",
                        column: x => x.BlockerId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Friendships_UserLowLtUserHigh",
                table: "Friendships",
                sql: "\"UserLowId\" < \"UserHighId\"");

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_AddresseeId",
                table: "FriendRequests",
                column: "AddresseeId");

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_RequesterId_AddresseeId",
                table: "FriendRequests",
                columns: new[] { "RequesterId", "AddresseeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserBlocks_BlockedUserId",
                table: "UserBlocks",
                column: "BlockedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBlocks_BlockerId_BlockedUserId",
                table: "UserBlocks",
                columns: new[] { "BlockerId", "BlockedUserId" },
                unique: true);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Friendships_Users_UserHighId",
                table: "Friendships");

            migrationBuilder.DropForeignKey(
                name: "FK_Friendships_Users_UserLowId",
                table: "Friendships");

            migrationBuilder.DropTable(
                name: "FriendRequests");

            migrationBuilder.DropTable(
                name: "UserBlocks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Friendships_UserLowLtUserHigh",
                table: "Friendships");

            migrationBuilder.RenameColumn(
                name: "UserLowId",
                table: "Friendships",
                newName: "RequesterId");

            migrationBuilder.RenameColumn(
                name: "UserHighId",
                table: "Friendships",
                newName: "AddresseeId");

            migrationBuilder.RenameIndex(
                name: "IX_Friendships_UserLowId_UserHighId",
                table: "Friendships",
                newName: "IX_Friendships_RequesterId_AddresseeId");

            migrationBuilder.RenameIndex(
                name: "IX_Friendships_UserHighId",
                table: "Friendships",
                newName: "IX_Friendships_AddresseeId");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Friendships",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_Friendships_Users_AddresseeId",
                table: "Friendships",
                column: "AddresseeId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Friendships_Users_RequesterId",
                table: "Friendships",
                column: "RequesterId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
