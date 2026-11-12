using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAAMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidenceContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EvidenceContents",
                columns: table => new
                {
                    EvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceContents", x => x.EvidenceId);
                    table.ForeignKey(
                        name: "FK_EvidenceContents_Evidence_EvidenceId",
                        column: x => x.EvidenceId,
                        principalTable: "Evidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM dbo.EvidenceContents)
                    THROW 51000, 'Cannot remove EvidenceContents while uploaded files exist.', 1;
                """);
            migrationBuilder.DropTable(
                name: "EvidenceContents");
        }
    }
}
