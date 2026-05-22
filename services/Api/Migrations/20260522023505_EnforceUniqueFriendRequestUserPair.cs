using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueFriendRequestUserPair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "FriendRequests" a
                USING "FriendRequests" b
                WHERE a."Id" > b."Id"
                  AND LEAST(a."RequesterId", a."AddresseeId") = LEAST(b."RequesterId", b."AddresseeId")
                  AND GREATEST(a."RequesterId", a."AddresseeId") = GREATEST(b."RequesterId", b."AddresseeId");
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "IX_FriendRequests_UserPair"
                ON "FriendRequests" (
                    LEAST("RequesterId", "AddresseeId"),
                    GREATEST("RequesterId", "AddresseeId"));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_FriendRequests_UserPair";""");
        }
    }
}
