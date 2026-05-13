using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HomeGroup.API.Migrations
{
    /// <inheritdoc />
    public partial class AddNextMeetingOverrideAndMeetingMeta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NextMeetingOverrideDate",
                table: "HomeGroups",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AttendanceMetas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HomeGroupId = table.Column<long>(type: "bigint", nullable: false),
                    MeetingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    GuestCount = table.Column<int>(type: "integer", nullable: false),
                    GuestInfo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceMetas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceMetas_HomeGroups_HomeGroupId",
                        column: x => x.HomeGroupId,
                        principalTable: "HomeGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceMetas_HomeGroupId_MeetingDate",
                table: "AttendanceMetas",
                columns: new[] { "HomeGroupId", "MeetingDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceMetas");

            migrationBuilder.DropColumn(
                name: "NextMeetingOverrideDate",
                table: "HomeGroups");
        }
    }
}
