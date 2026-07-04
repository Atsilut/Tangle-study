using Api.Global.Db;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260704120000_RemoveMonolithCommunityTables")]
    public class RemoveMonolithCommunityTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Comments");
            migrationBuilder.DropTable(name: "Posts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Posts/comments now live in the community schema (community-service). Recreate public tables only if rolling back.
            throw new NotSupportedException(
                "Cannot recreate public Posts/Comments tables; restore from backup or re-run community-service migration.");
        }
    }
}
