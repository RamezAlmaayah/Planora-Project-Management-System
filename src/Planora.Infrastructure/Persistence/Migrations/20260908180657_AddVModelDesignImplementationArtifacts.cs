using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVModelDesignImplementationArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DesignArtifacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Identifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesignArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DesignArtifacts_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImplementationArtifacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Identifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImplementationArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImplementationArtifacts_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DesignArtifactRequirements",
                columns: table => new
                {
                    DesignArtifactId = table.Column<int>(type: "int", nullable: false),
                    RequirementId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesignArtifactRequirements", x => new { x.DesignArtifactId, x.RequirementId });
                    table.ForeignKey(
                        name: "FK_DesignArtifactRequirements_DesignArtifacts_DesignArtifactId",
                        column: x => x.DesignArtifactId,
                        principalTable: "DesignArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DesignArtifactRequirements_Requirements_RequirementId",
                        column: x => x.RequirementId,
                        principalTable: "Requirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImplementationArtifactDesigns",
                columns: table => new
                {
                    ImplementationArtifactId = table.Column<int>(type: "int", nullable: false),
                    DesignArtifactId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImplementationArtifactDesigns", x => new { x.ImplementationArtifactId, x.DesignArtifactId });
                    table.ForeignKey(
                        name: "FK_ImplementationArtifactDesigns_DesignArtifacts_DesignArtifactId",
                        column: x => x.DesignArtifactId,
                        principalTable: "DesignArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImplementationArtifactDesigns_ImplementationArtifacts_ImplementationArtifactId",
                        column: x => x.ImplementationArtifactId,
                        principalTable: "ImplementationArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DesignArtifactRequirements_RequirementId",
                table: "DesignArtifactRequirements",
                column: "RequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_DesignArtifacts_ProjectId_Identifier",
                table: "DesignArtifacts",
                columns: new[] { "ProjectId", "Identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DesignArtifacts_ProjectId_Type",
                table: "DesignArtifacts",
                columns: new[] { "ProjectId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_ImplementationArtifactDesigns_DesignArtifactId",
                table: "ImplementationArtifactDesigns",
                column: "DesignArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_ImplementationArtifacts_ProjectId_Identifier",
                table: "ImplementationArtifacts",
                columns: new[] { "ProjectId", "Identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImplementationArtifacts_ProjectId_Type",
                table: "ImplementationArtifacts",
                columns: new[] { "ProjectId", "Type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DesignArtifactRequirements");

            migrationBuilder.DropTable(
                name: "ImplementationArtifactDesigns");

            migrationBuilder.DropTable(
                name: "DesignArtifacts");

            migrationBuilder.DropTable(
                name: "ImplementationArtifacts");
        }
    }
}
