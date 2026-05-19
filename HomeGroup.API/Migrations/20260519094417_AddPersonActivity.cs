using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HomeGroup.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PersonActivities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    AuthorId = table.Column<long>(type: "bigint", nullable: true),
                    OldStatusId = table.Column<long>(type: "bigint", nullable: true),
                    OldStatusName = table.Column<string>(type: "text", nullable: true),
                    OldStatusColor = table.Column<string>(type: "text", nullable: true),
                    NewStatusId = table.Column<long>(type: "bigint", nullable: true),
                    NewStatusName = table.Column<string>(type: "text", nullable: true),
                    NewStatusColor = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonActivities_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PersonActivities_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersonActivities_AuthorId",
                table: "PersonActivities",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonActivities_PersonId",
                table: "PersonActivities",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonActivities");
        }
    }
}
