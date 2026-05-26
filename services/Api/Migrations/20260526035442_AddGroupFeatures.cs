using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "GroupMembers",
                newName: "JoinedAt");

            migrationBuilder.AddColumn<long>(
                name: "GroupBoardId",
                table: "Posts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "GroupId",
                table: "Posts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "JoinPolicy",
                table: "Groups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "GroupApplications",
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
                        principalTable: "Groups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GroupApplications_Users_ApplicantId",
                        column: x => x.ApplicantId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupBlacklists",
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
                        principalTable: "Groups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GroupBlacklists_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupBoards",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GroupId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Visibility = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupBoards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupBoards_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GroupInvitations",
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
                        principalTable: "Groups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GroupInvitations_Users_InviteeId",
                        column: x => x.InviteeId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupInvitations_Users_InviterId",
                        column: x => x.InviterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_GroupBoardId",
                table: "Posts",
                column: "GroupBoardId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_GroupId",
                table: "Posts",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupApplications_ApplicantId",
                table: "GroupApplications",
                column: "ApplicantId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupApplications_GroupId_ApplicantId",
                table: "GroupApplications",
                columns: new[] { "GroupId", "ApplicantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupBlacklists_GroupId_UserId",
                table: "GroupBlacklists",
                columns: new[] { "GroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupBlacklists_UserId",
                table: "GroupBlacklists",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupBoards_GroupId_Name",
                table: "GroupBoards",
                columns: new[] { "GroupId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvitations_GroupId_InviteeId",
                table: "GroupInvitations",
                columns: new[] { "GroupId", "InviteeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvitations_InviteeId",
                table: "GroupInvitations",
                column: "InviteeId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvitations_InviterId",
                table: "GroupInvitations",
                column: "InviterId");

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_GroupBoards_GroupBoardId",
                table: "Posts",
                column: "GroupBoardId",
                principalTable: "GroupBoards",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_Groups_GroupId",
                table: "Posts",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Posts_GroupBoards_GroupBoardId",
                table: "Posts");

            migrationBuilder.DropForeignKey(
                name: "FK_Posts_Groups_GroupId",
                table: "Posts");

            migrationBuilder.DropTable(
                name: "GroupApplications");

            migrationBuilder.DropTable(
                name: "GroupBlacklists");

            migrationBuilder.DropTable(
                name: "GroupBoards");

            migrationBuilder.DropTable(
                name: "GroupInvitations");

            migrationBuilder.DropIndex(
                name: "IX_Posts_GroupBoardId",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Posts_GroupId",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "GroupBoardId",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "JoinPolicy",
                table: "Groups");

            migrationBuilder.RenameColumn(
                name: "JoinedAt",
                table: "GroupMembers",
                newName: "CreatedAt");
        }
    }
}
