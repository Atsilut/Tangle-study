using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupIdToLocationSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Friend-scoped sessions cannot be mapped to a group; clear them before adding GroupId.
            migrationBuilder.Sql("DELETE FROM \"LocationSessions\";");

            migrationBuilder.DropIndex(
                name: "IX_LocationSessions_UserId_EndedAt",
                table: "LocationSessions");

            migrationBuilder.AddColumn<long>(
                name: "GroupId",
                table: "LocationSessions",
                type: "bigint",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_LocationSessions_GroupId_EndedAt",
                table: "LocationSessions",
                columns: new[] { "GroupId", "EndedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LocationSessions_UserId_GroupId_EndedAt",
                table: "LocationSessions",
                columns: new[] { "UserId", "GroupId", "EndedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_LocationSessions_Groups_GroupId",
                table: "LocationSessions",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LocationSessions_Groups_GroupId",
                table: "LocationSessions");

            migrationBuilder.DropIndex(
                name: "IX_LocationSessions_GroupId_EndedAt",
                table: "LocationSessions");

            migrationBuilder.DropIndex(
                name: "IX_LocationSessions_UserId_GroupId_EndedAt",
                table: "LocationSessions");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "LocationSessions");

            migrationBuilder.CreateIndex(
                name: "IX_LocationSessions_UserId_EndedAt",
                table: "LocationSessions",
                columns: new[] { "UserId", "EndedAt" });
        }
    }
}
