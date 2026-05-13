using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGroup.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonExtendedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "People",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Church",
                table: "People",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "People",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBaptized",
                table: "People",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsBaptizedWithSpirit",
                table: "People",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MaritalStatus",
                table: "People",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ministry",
                table: "People",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telegram",
                table: "People",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "People");

            migrationBuilder.DropColumn(
                name: "Church",
                table: "People");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "People");

            migrationBuilder.DropColumn(
                name: "IsBaptized",
                table: "People");

            migrationBuilder.DropColumn(
                name: "IsBaptizedWithSpirit",
                table: "People");

            migrationBuilder.DropColumn(
                name: "MaritalStatus",
                table: "People");

            migrationBuilder.DropColumn(
                name: "Ministry",
                table: "People");

            migrationBuilder.DropColumn(
                name: "Telegram",
                table: "People");
        }
    }
}
