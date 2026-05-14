using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGroup.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingEndTimeAndAutoBook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AutoBookRoomId",
                table: "HomeGroups",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeetingEndTime",
                table: "HomeGroups",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomeGroups_AutoBookRoomId",
                table: "HomeGroups",
                column: "AutoBookRoomId");

            migrationBuilder.AddForeignKey(
                name: "FK_HomeGroups_Rooms_AutoBookRoomId",
                table: "HomeGroups",
                column: "AutoBookRoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HomeGroups_Rooms_AutoBookRoomId",
                table: "HomeGroups");

            migrationBuilder.DropIndex(
                name: "IX_HomeGroups_AutoBookRoomId",
                table: "HomeGroups");

            migrationBuilder.DropColumn(
                name: "AutoBookRoomId",
                table: "HomeGroups");

            migrationBuilder.DropColumn(
                name: "MeetingEndTime",
                table: "HomeGroups");
        }
    }
}
