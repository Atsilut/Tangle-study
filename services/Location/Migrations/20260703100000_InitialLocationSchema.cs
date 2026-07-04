using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Location.Migrations
{
    public partial class InitialLocationSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "location");

            migrationBuilder.CreateTable(
                name: "MapPins",
                schema: "location",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    DeletedUserId = table.Column<long>(type: "bigint", nullable: true),
                    PostId = table.Column<long>(type: "bigint", nullable: true),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapPins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocationSessions",
                schema: "location",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    DeletedUserId = table.Column<long>(type: "bigint", nullable: true),
                    GroupId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MapPins_Latitude_Longitude",
                schema: "location",
                table: "MapPins",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_MapPins_PostId",
                schema: "location",
                table: "MapPins",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_MapPins_UserId",
                schema: "location",
                table: "MapPins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationSessions_GroupId_EndedAt",
                schema: "location",
                table: "LocationSessions",
                columns: new[] { "GroupId", "EndedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LocationSessions_UserId_GroupId",
                schema: "location",
                table: "LocationSessions",
                columns: new[] { "UserId", "GroupId" },
                unique: true,
                filter: "\"EndedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LocationSessions_UserId_GroupId_EndedAt",
                schema: "location",
                table: "LocationSessions",
                columns: new[] { "UserId", "GroupId", "EndedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "LocationSessions", schema: "location");
            migrationBuilder.DropTable(name: "MapPins", schema: "location");
        }
    }
}
