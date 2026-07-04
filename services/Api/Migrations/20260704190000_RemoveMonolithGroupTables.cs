using Api.Global.Db;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260704190000_RemoveMonolithGroupTables")]
    public class RemoveMonolithGroupTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "GroupApplications");
            migrationBuilder.DropTable(name: "GroupBlacklists");
            migrationBuilder.DropTable(name: "GroupBoards");
            migrationBuilder.DropTable(name: "GroupInvitations");
            migrationBuilder.DropTable(name: "GroupMembers");
            migrationBuilder.DropTable(name: "Groups");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Cannot recreate public group tables; restore from backup or re-run group-service migration.");
        }
    }
}
