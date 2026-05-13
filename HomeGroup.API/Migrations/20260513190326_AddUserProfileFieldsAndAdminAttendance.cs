using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGroup.API.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileFieldsAndAdminAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Attendances_PersonId_HomeGroupId_MeetingDate",
                table: "Attendances");

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Church",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "Users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBaptized",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsBaptizedWithSpirit",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MaritalStatus",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ministry",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PersonStatusId",
                table: "Users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telegram",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "PersonId",
                table: "Attendances",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "UserId",
                table: "Attendances",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PersonStatusId",
                table: "Users",
                column: "PersonStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_PersonId_HomeGroupId_MeetingDate",
                table: "Attendances",
                columns: new[] { "PersonId", "HomeGroupId", "MeetingDate" },
                unique: true,
                filter: "\"PersonId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_UserId_HomeGroupId_MeetingDate",
                table: "Attendances",
                columns: new[] { "UserId", "HomeGroupId", "MeetingDate" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Attendances_Users_UserId",
                table: "Attendances",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_PersonStatuses_PersonStatusId",
                table: "Users",
                column: "PersonStatusId",
                principalTable: "PersonStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attendances_Users_UserId",
                table: "Attendances");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_PersonStatuses_PersonStatusId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_PersonStatusId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Attendances_PersonId_HomeGroupId_MeetingDate",
                table: "Attendances");

            migrationBuilder.DropIndex(
                name: "IX_Attendances_UserId_HomeGroupId_MeetingDate",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Church",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsBaptized",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsBaptizedWithSpirit",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MaritalStatus",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Ministry",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PersonStatusId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Telegram",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Attendances");

            migrationBuilder.AlterColumn<long>(
                name: "PersonId",
                table: "Attendances",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_PersonId_HomeGroupId_MeetingDate",
                table: "Attendances",
                columns: new[] { "PersonId", "HomeGroupId", "MeetingDate" },
                unique: true);
        }
    }
}
