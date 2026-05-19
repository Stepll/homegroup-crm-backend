using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGroup.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonActivityOversightChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NewValue",
                table: "PersonActivities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OldValue",
                table: "PersonActivities",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NewValue",
                table: "PersonActivities");

            migrationBuilder.DropColumn(
                name: "OldValue",
                table: "PersonActivities");
        }
    }
}
