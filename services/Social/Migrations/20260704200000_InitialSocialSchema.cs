using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Social.Migrations;

/// <inheritdoc />
public partial class InitialSocialSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "social");

        migrationBuilder.CreateTable(
            name: "Friendships",
            schema: "social",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UserLowId = table.Column<long>(type: "bigint", nullable: false),
                UserHighId = table.Column<long>(type: "bigint", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Friendships", x => x.Id);
                table.CheckConstraint("CK_Friendships_UserLowLtUserHigh", "\"UserLowId\" < \"UserHighId\"");
            });

        migrationBuilder.CreateTable(
            name: "FriendRequests",
            schema: "social",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RequesterId = table.Column<long>(type: "bigint", nullable: false),
                AddresseeId = table.Column<long>(type: "bigint", nullable: false),
                IsPending = table.Column<bool>(type: "boolean", nullable: false),
                IgnoredByBlock = table.Column<bool>(type: "boolean", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FriendRequests", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "UserBlocks",
            schema: "social",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                BlockerId = table.Column<long>(type: "bigint", nullable: false),
                BlockedUserId = table.Column<long>(type: "bigint", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserBlocks", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Friendships_UserHighId",
            schema: "social",
            table: "Friendships",
            column: "UserHighId");

        migrationBuilder.CreateIndex(
            name: "IX_Friendships_UserLowId",
            schema: "social",
            table: "Friendships",
            column: "UserLowId");

        migrationBuilder.CreateIndex(
            name: "IX_Friendships_UserLowId_UserHighId",
            schema: "social",
            table: "Friendships",
            columns: ["UserLowId", "UserHighId"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_FriendRequests_AddresseeId",
            schema: "social",
            table: "FriendRequests",
            column: "AddresseeId");

        migrationBuilder.CreateIndex(
            name: "IX_FriendRequests_RequesterId",
            schema: "social",
            table: "FriendRequests",
            column: "RequesterId");

        migrationBuilder.CreateIndex(
            name: "IX_FriendRequests_RequesterId_AddresseeId",
            schema: "social",
            table: "FriendRequests",
            columns: ["RequesterId", "AddresseeId"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserBlocks_BlockedUserId",
            schema: "social",
            table: "UserBlocks",
            column: "BlockedUserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserBlocks_BlockerId",
            schema: "social",
            table: "UserBlocks",
            column: "BlockerId");

        migrationBuilder.CreateIndex(
            name: "IX_UserBlocks_BlockerId_BlockedUserId",
            schema: "social",
            table: "UserBlocks",
            columns: ["BlockerId", "BlockedUserId"],
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FriendRequests", schema: "social");
        migrationBuilder.DropTable(name: "Friendships", schema: "social");
        migrationBuilder.DropTable(name: "UserBlocks", schema: "social");
    }
}
