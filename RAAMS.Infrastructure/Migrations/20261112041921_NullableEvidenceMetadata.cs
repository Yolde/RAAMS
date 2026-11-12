using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAAMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NullableEvidenceMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SharePointUrl",
                table: "Evidence",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "SharePointItemId",
                table: "Evidence",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "FileHash",
                table: "Evidence",
                type: "char(64)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "char(64)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SharePointUrl",
                table: "Evidence",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SharePointItemId",
                table: "Evidence",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FileHash",
                table: "Evidence",
                type: "char(64)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "char(64)",
                oldNullable: true);
        }
    }
}
