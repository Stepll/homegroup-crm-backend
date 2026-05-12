using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HomeGroup.API.Migrations
{
    /// <inheritdoc />
    public partial class PersonNewFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "People",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "People",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OversightInfo",
                table: "People",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PrimaryGroupId",
                table: "People",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PersonCustomFields",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonCustomFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonCustomFields_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_People_PrimaryGroupId",
                table: "People",
                column: "PrimaryGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonCustomFields_PersonId",
                table: "PersonCustomFields",
                column: "PersonId");

            migrationBuilder.AddForeignKey(
                name: "FK_People_HomeGroups_PrimaryGroupId",
                table: "People",
                column: "PrimaryGroupId",
                principalTable: "HomeGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_People_HomeGroups_PrimaryGroupId",
                table: "People");

            migrationBuilder.DropTable(
                name: "PersonCustomFields");

            migrationBuilder.DropIndex(
                name: "IX_People_PrimaryGroupId",
                table: "People");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "People");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "People");

            migrationBuilder.DropColumn(
                name: "OversightInfo",
                table: "People");

            migrationBuilder.DropColumn(
                name: "PrimaryGroupId",
                table: "People");
        }
    }
}
