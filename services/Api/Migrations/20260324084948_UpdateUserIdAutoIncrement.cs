using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserIdAutoIncrement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Users",
                newName: "LegacyId");

            migrationBuilder.AddColumn<long>(
                name: "Id",
                table: "Users",
                type: "bigint",
                nullable: true)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.Sql(@"
                UPDATE ""Users""
                SET ""Id"" = nextval(pg_get_serial_sequence('""Users""', 'Id'))
                WHERE ""Id"" IS NULL;
            ");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "Users");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "Users",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.AddColumn<Guid>(
                name: "LegacyId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(@"
                WITH uuid_source AS (
                    SELECT ""Id"", lpad(to_hex(""Id""), 32, '0') AS hex
                    FROM ""Users""
                )
                UPDATE ""Users"" u
                SET ""LegacyId"" = (
                    substr(s.hex, 1, 8) || '-' ||
                    substr(s.hex, 9, 4) || '-' ||
                    substr(s.hex, 13, 4) || '-' ||
                    substr(s.hex, 17, 4) || '-' ||
                    substr(s.hex, 21, 12)
                )::uuid
                FROM uuid_source s
                WHERE s.""Id"" = u.""Id"";
            ");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "LegacyId",
                table: "Users",
                newName: "Id");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "Users",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "Id");
        }
    }
}
