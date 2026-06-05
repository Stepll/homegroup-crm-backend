using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGroup.API.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalMeetingDate",
                table: "MeetingPlans",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MovedFromDate",
                table: "CalendarEvents",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MovedToDate",
                table: "CalendarEvents",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalMeetingDate",
                table: "MeetingPlans");

            migrationBuilder.DropColumn(
                name: "MovedFromDate",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "MovedToDate",
                table: "CalendarEvents");
        }
    }
}
