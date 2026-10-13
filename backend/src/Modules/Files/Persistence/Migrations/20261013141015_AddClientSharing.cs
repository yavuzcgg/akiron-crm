using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akiron.Modules.Files.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "client_uploader_name",
                schema: "files",
                table: "stored_files",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "shared_with_client",
                schema: "files",
                table: "stored_files",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "client_uploader_name",
                schema: "files",
                table: "stored_files");

            migrationBuilder.DropColumn(
                name: "shared_with_client",
                schema: "files",
                table: "stored_files");
        }
    }
}
