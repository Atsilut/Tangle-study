using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardWriteability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "GroupBoards"
                ADD COLUMN IF NOT EXISTS "Writeability" integer NOT NULL DEFAULT 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "GroupBoards"
                DROP COLUMN IF EXISTS "Writeability";
                """);
        }
    }
}
