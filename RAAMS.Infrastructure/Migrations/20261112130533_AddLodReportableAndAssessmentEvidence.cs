using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAAMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLodReportableAndAssessmentEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ActionId",
                table: "Evidence",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "AssessmentId",
                table: "Evidence",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LineOfDefence",
                table: "Assessments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "LOD1");

            migrationBuilder.AddColumn<string>(
                name: "Opinion",
                table: "Assessments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "NotRated");

            migrationBuilder.AddColumn<string>(
                name: "Reportable",
                table: "Actions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NonCEO");

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_AssessmentId",
                table: "Evidence",
                column: "AssessmentId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Evidence_OneParent",
                table: "Evidence",
                sql: "[ActionId] IS NOT NULL OR [AssessmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_LineOfDefence",
                table: "Assessments",
                column: "LineOfDefence");

            migrationBuilder.CreateIndex(
                name: "IX_Actions_Reportable",
                table: "Actions",
                column: "Reportable");

            migrationBuilder.AddForeignKey(
                name: "FK_Evidence_Assessments_AssessmentId",
                table: "Evidence",
                column: "AssessmentId",
                principalTable: "Assessments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM dbo.Evidence WHERE ActionId IS NULL)
                    THROW 50000, 'Cannot reverse migration while assessment-only evidence exists.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Evidence_Assessments_AssessmentId",
                table: "Evidence");

            migrationBuilder.DropIndex(
                name: "IX_Evidence_AssessmentId",
                table: "Evidence");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Evidence_OneParent",
                table: "Evidence");

            migrationBuilder.DropIndex(
                name: "IX_Assessments_LineOfDefence",
                table: "Assessments");

            migrationBuilder.DropIndex(
                name: "IX_Actions_Reportable",
                table: "Actions");

            migrationBuilder.DropColumn(
                name: "AssessmentId",
                table: "Evidence");

            migrationBuilder.DropColumn(
                name: "LineOfDefence",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "Opinion",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "Reportable",
                table: "Actions");

            migrationBuilder.AlterColumn<Guid>(
                name: "ActionId",
                table: "Evidence",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
