using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRequirementTypeSpecificFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExceptionScenario",
                table: "Requirements",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NfrCategory",
                table: "Requirements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Preconditions",
                table: "Requirements",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExceptionScenario",
                table: "Requirements");

            migrationBuilder.DropColumn(
                name: "NfrCategory",
                table: "Requirements");

            migrationBuilder.DropColumn(
                name: "Preconditions",
                table: "Requirements");
        }
    }
}
