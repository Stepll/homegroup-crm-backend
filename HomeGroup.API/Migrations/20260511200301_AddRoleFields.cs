using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGroup.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "Roles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "Roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "Roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PermissionsJson",
                table: "Roles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "Color", "IsDefault", "IsSystem", "PermissionsJson" },
                values: new object[] { "#2AAFCA", false, true, "[\"*\"]" });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2L,
                columns: new[] { "Color", "IsDefault", "IsSystem", "PermissionsJson" },
                values: new object[] { "#6366F1", true, true, "[\"dashboard\",\"people\",\"groups\",\"admins\"]" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Color",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "IsSystem",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "PermissionsJson",
                table: "Roles");
        }
    }
}
