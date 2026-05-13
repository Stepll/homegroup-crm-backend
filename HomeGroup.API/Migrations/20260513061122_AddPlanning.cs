using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HomeGroup.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MeetingPlans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HomeGroupId = table.Column<long>(type: "bigint", nullable: false),
                    MeetingDate = table.Column<string>(type: "text", nullable: false),
                    AppliedTemplateName = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingPlans_HomeGroups_HomeGroupId",
                        column: x => x.HomeGroupId,
                        principalTable: "HomeGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MeetingPlanBlocks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlanId = table.Column<long>(type: "bigint", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Time = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Info = table.Column<string>(type: "text", nullable: true),
                    Responsible = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingPlanBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingPlanBlocks_MeetingPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "MeetingPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanTemplateBlocks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TemplateId = table.Column<long>(type: "bigint", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Time = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Info = table.Column<string>(type: "text", nullable: true),
                    Responsible = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanTemplateBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanTemplateBlocks_PlanTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "PlanTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingPlanBlocks_PlanId",
                table: "MeetingPlanBlocks",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingPlans_HomeGroupId_MeetingDate",
                table: "MeetingPlans",
                columns: new[] { "HomeGroupId", "MeetingDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanTemplateBlocks_TemplateId",
                table: "PlanTemplateBlocks",
                column: "TemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetingPlanBlocks");

            migrationBuilder.DropTable(
                name: "PlanTemplateBlocks");

            migrationBuilder.DropTable(
                name: "MeetingPlans");

            migrationBuilder.DropTable(
                name: "PlanTemplates");
        }
    }
}
