using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HomeGroup.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "People",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_People", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HomeGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    MeetingDay = table.Column<string>(type: "text", nullable: true),
                    MeetingTime = table.Column<string>(type: "text", nullable: true),
                    Location = table.Column<string>(type: "text", nullable: true),
                    LeaderId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeGroups_People_LeaderId",
                        column: x => x.LeaderId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Attendances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    HomeGroupId = table.Column<int>(type: "integer", nullable: false),
                    MeetingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WasPresent = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attendances_HomeGroups_HomeGroupId",
                        column: x => x.HomeGroupId,
                        principalTable: "HomeGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Attendances_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HomeGroupMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    HomeGroupId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeGroupMembers_HomeGroups_HomeGroupId",
                        column: x => x.HomeGroupId,
                        principalTable: "HomeGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HomeGroupMembers_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_HomeGroupId",
                table: "Attendances",
                column: "HomeGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_PersonId_HomeGroupId_MeetingDate",
                table: "Attendances",
                columns: new[] { "PersonId", "HomeGroupId", "MeetingDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomeGroupMembers_HomeGroupId",
                table: "HomeGroupMembers",
                column: "HomeGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeGroupMembers_PersonId_HomeGroupId",
                table: "HomeGroupMembers",
                columns: new[] { "PersonId", "HomeGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomeGroups_LeaderId",
                table: "HomeGroups",
                column: "LeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Attendances");

            migrationBuilder.DropTable(
                name: "HomeGroupMembers");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "HomeGroups");

            migrationBuilder.DropTable(
                name: "People");
        }
    }
}
