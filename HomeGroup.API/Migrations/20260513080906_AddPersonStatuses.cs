using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HomeGroup.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "People");

            migrationBuilder.AddColumn<long>(
                name: "PersonStatusId",
                table: "People",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PersonStatuses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonStatuses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_People_PersonStatusId",
                table: "People",
                column: "PersonStatusId");

            migrationBuilder.AddForeignKey(
                name: "FK_People_PersonStatuses_PersonStatusId",
                table: "People",
                column: "PersonStatusId",
                principalTable: "PersonStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_People_PersonStatuses_PersonStatusId",
                table: "People");

            migrationBuilder.DropTable(
                name: "PersonStatuses");

            migrationBuilder.DropIndex(
                name: "IX_People_PersonStatusId",
                table: "People");

            migrationBuilder.DropColumn(
                name: "PersonStatusId",
                table: "People");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "People",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
