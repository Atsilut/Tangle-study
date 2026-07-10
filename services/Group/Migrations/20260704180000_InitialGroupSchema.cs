using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Group.Migrations
{
    /// <inheritdoc />
    public partial class InitialGroupSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "group");

            migrationBuilder.CreateTable(
                name: "Groups",
                schema: "group",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Visibility = table.Column<int>(type: "integer", nullable: false),
                    JoinPolicy = table.Column<int>(type: "integer", nullable: false),
                    InvitePolicy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupApplications",
                schema: "group",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GroupId = table.Column<long>(type: "bigint", nullable: false),
                    ApplicantId = table.Column<long>(type: "bigint", nullable: false),
                    IsPending = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupApplications_Groups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "group",
                        principalTable: "Groups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GroupBlacklists",
                schema: "group",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GroupId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupBlacklists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupBlacklists_Groups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "group",
                        principalTable: "Groups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GroupBoards",
                schema: "group",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GroupId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Visibility = table.Column<int>(type: "integer", nullable: false),
                    Writeability = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupBoards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupBoards_Groups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "group",
                        principalTable: "Groups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GroupInvitations",
                schema: "group",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GroupId = table.Column<long>(type: "bigint", nullable: false),
                    InviterId = table.Column<long>(type: "bigint", nullable: false),
                    InviteeId = table.Column<long>(type: "bigint", nullable: false),
                    IsPending = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupInvitations_Groups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "group",
                        principalTable: "Groups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GroupMembers",
                schema: "group",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GroupId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupMembers_Groups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "group",
                        principalTable: "Groups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupApplications_ApplicantId",
                schema: "group",
                table: "GroupApplications",
                column: "ApplicantId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupApplications_GroupId_ApplicantId",
                schema: "group",
                table: "GroupApplications",
                columns: new[] { "GroupId", "ApplicantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupBlacklists_GroupId_UserId",
                schema: "group",
                table: "GroupBlacklists",
                columns: new[] { "GroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupBlacklists_UserId",
                schema: "group",
                table: "GroupBlacklists",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupBoards_GroupId_Name",
                schema: "group",
                table: "GroupBoards",
                columns: new[] { "GroupId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvitations_GroupId_InviteeId",
                schema: "group",
                table: "GroupInvitations",
                columns: new[] { "GroupId", "InviteeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvitations_InviteeId",
                schema: "group",
                table: "GroupInvitations",
                column: "InviteeId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvitations_InviterId",
                schema: "group",
                table: "GroupInvitations",
                column: "InviterId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMembers_GroupId_UserId",
                schema: "group",
                table: "GroupMembers",
                columns: new[] { "GroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupMembers_UserId",
                schema: "group",
                table: "GroupMembers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "GroupApplications", schema: "group");
            migrationBuilder.DropTable(name: "GroupBlacklists", schema: "group");
            migrationBuilder.DropTable(name: "GroupBoards", schema: "group");
            migrationBuilder.DropTable(name: "GroupInvitations", schema: "group");
            migrationBuilder.DropTable(name: "GroupMembers", schema: "group");
            migrationBuilder.DropTable(name: "Groups", schema: "group");
        }
    }
}
