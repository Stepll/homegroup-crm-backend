using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HomeGroup.API.Migrations
{
    /// <inheritdoc />
    public partial class AddUserActivityFieldsAndUserCustomFieldValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "UserActivities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewValue",
                table: "UserActivities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OldValue",
                table: "UserActivities",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserCustomFieldValues",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    FieldId = table.Column<long>(type: "bigint", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCustomFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCustomFieldValues_HomeGroupCustomFields_FieldId",
                        column: x => x.FieldId,
                        principalTable: "HomeGroupCustomFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCustomFieldValues_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCustomFieldValues_FieldId",
                table: "UserCustomFieldValues",
                column: "FieldId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCustomFieldValues_UserId_FieldId",
                table: "UserCustomFieldValues",
                columns: new[] { "UserId", "FieldId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCustomFieldValues");

            migrationBuilder.DropColumn(
                name: "Content",
                table: "UserActivities");

            migrationBuilder.DropColumn(
                name: "NewValue",
                table: "UserActivities");

            migrationBuilder.DropColumn(
                name: "OldValue",
                table: "UserActivities");
        }
    }
}
