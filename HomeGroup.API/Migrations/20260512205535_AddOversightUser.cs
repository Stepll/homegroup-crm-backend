using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGroup.API.Migrations
{
    /// <inheritdoc />
    public partial class AddOversightUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "OversightUserId",
                table: "People",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_People_OversightUserId",
                table: "People",
                column: "OversightUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_People_Users_OversightUserId",
                table: "People",
                column: "OversightUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_People_Users_OversightUserId",
                table: "People");

            migrationBuilder.DropIndex(
                name: "IX_People_OversightUserId",
                table: "People");

            migrationBuilder.DropColumn(
                name: "OversightUserId",
                table: "People");
        }
    }
}
