using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HomeGroup.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramChatIdAndAnonymousPolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TelegramChatId",
                table: "Users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TelegramChatId",
                table: "People",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnonymousPolls",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HomeGroupId = table.Column<long>(type: "bigint", nullable: false),
                    StartedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    DestinationChatId = table.Column<long>(type: "bigint", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnonymousPolls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnonymousPolls_HomeGroups_HomeGroupId",
                        column: x => x.HomeGroupId,
                        principalTable: "HomeGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnonymousPolls_Users_StartedByUserId",
                        column: x => x.StartedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnonymousPolls_HomeGroupId_ExpiresAt",
                table: "AnonymousPolls",
                columns: new[] { "HomeGroupId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnonymousPolls_StartedByUserId",
                table: "AnonymousPolls",
                column: "StartedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnonymousPolls");

            migrationBuilder.DropColumn(
                name: "TelegramChatId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TelegramChatId",
                table: "People");
        }
    }
}
