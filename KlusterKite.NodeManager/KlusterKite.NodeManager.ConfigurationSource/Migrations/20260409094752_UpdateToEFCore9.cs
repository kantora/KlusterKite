using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KlusterKite.NodeManager.ConfigurationSource.Migrations
{
    /// <inheritdoc />
    public partial class UpdateToEFCore9 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropUniqueConstraint(
                name: "AK_RoleUsers_Id",
                table: "RoleUsers");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "RoleUsers",
                type: "serial",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "serial")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Migrations",
                type: "serial",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "serial")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "MigrationLogRecords",
                type: "serial",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "serial")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Configurations",
                type: "serial",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "serial")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "CompatibleTemplate",
                type: "serial",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "serial")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "RoleUsers",
                type: "serial",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "serial")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Migrations",
                type: "serial",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "serial")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "MigrationLogRecords",
                type: "serial",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "serial")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Configurations",
                type: "serial",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "serial")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "CompatibleTemplate",
                type: "serial",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "serial")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_RoleUsers_Id",
                table: "RoleUsers",
                column: "Id");
        }
    }
}
