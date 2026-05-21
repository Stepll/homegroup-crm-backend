using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGroup.API.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupNeedPersonLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PersonId",
                table: "GroupNeeds",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "UserId",
                table: "GroupNeeds",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupNeeds_PersonId",
                table: "GroupNeeds",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupNeeds_UserId",
                table: "GroupNeeds",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupNeeds_People_PersonId",
                table: "GroupNeeds",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupNeeds_Users_UserId",
                table: "GroupNeeds",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupNeeds_People_PersonId",
                table: "GroupNeeds");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupNeeds_Users_UserId",
                table: "GroupNeeds");

            migrationBuilder.DropIndex(
                name: "IX_GroupNeeds_PersonId",
                table: "GroupNeeds");

            migrationBuilder.DropIndex(
                name: "IX_GroupNeeds_UserId",
                table: "GroupNeeds");

            migrationBuilder.DropColumn(
                name: "PersonId",
                table: "GroupNeeds");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "GroupNeeds");
        }
    }
}
