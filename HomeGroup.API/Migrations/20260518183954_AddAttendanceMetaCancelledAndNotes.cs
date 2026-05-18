using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGroup.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceMetaCancelledAndNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "AttendanceMetas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "AttendanceMetas",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "AttendanceMetas");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "AttendanceMetas");
        }
    }
}
