using Api.Global.Db;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260704200000_RemoveMonolithSocialTables")]
    public class RemoveMonolithSocialTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "FriendRequests");
            migrationBuilder.DropTable(name: "Friendships");
            migrationBuilder.DropTable(name: "UserBlocks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Cannot recreate public social tables; restore from backup or re-run social-service migration.");
        }
    }
}
