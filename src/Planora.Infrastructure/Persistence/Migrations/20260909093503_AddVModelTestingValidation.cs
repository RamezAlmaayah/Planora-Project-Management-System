using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVModelTestingValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VModelPhases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    PhaseType = table.Column<int>(type: "int", nullable: false),
                    PhaseOrder = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VModelPhases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VModelPhases_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VModelTestCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Identifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    TestLevel = table.Column<int>(type: "int", nullable: false),
                    Preconditions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TestSteps = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    ExpectedResult = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VModelTestCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VModelTestCases_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VModelPhaseValidations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VModelPhaseId = table.Column<int>(type: "int", nullable: false),
                    Result = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    ValidatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ValidatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VModelPhaseValidations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VModelPhaseValidations_VModelPhases_VModelPhaseId",
                        column: x => x.VModelPhaseId,
                        principalTable: "VModelPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VModelTestCaseImplementationArtifacts",
                columns: table => new
                {
                    VModelTestCaseId = table.Column<int>(type: "int", nullable: false),
                    ImplementationArtifactId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VModelTestCaseImplementationArtifacts", x => new { x.VModelTestCaseId, x.ImplementationArtifactId });
                    table.ForeignKey(
                        name: "FK_VModelTestCaseImplementationArtifacts_ImplementationArtifacts_ImplementationArtifactId",
                        column: x => x.ImplementationArtifactId,
                        principalTable: "ImplementationArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VModelTestCaseImplementationArtifacts_VModelTestCases_VModelTestCaseId",
                        column: x => x.VModelTestCaseId,
                        principalTable: "VModelTestCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VModelTestExecutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VModelTestCaseId = table.Column<int>(type: "int", nullable: false),
                    Result = table.Column<int>(type: "int", nullable: false),
                    ActualResult = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    ExecutedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VModelTestExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VModelTestExecutions_VModelTestCases_VModelTestCaseId",
                        column: x => x.VModelTestCaseId,
                        principalTable: "VModelTestCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VModelTestExecutionIssues",
                columns: table => new
                {
                    VModelTestExecutionId = table.Column<int>(type: "int", nullable: false),
                    IssueId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VModelTestExecutionIssues", x => new { x.VModelTestExecutionId, x.IssueId });
                    table.ForeignKey(
                        name: "FK_VModelTestExecutionIssues_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VModelTestExecutionIssues_VModelTestExecutions_VModelTestExecutionId",
                        column: x => x.VModelTestExecutionId,
                        principalTable: "VModelTestExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VModelPhases_ProjectId_PhaseOrder",
                table: "VModelPhases",
                columns: new[] { "ProjectId", "PhaseOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VModelPhases_ProjectId_PhaseType",
                table: "VModelPhases",
                columns: new[] { "ProjectId", "PhaseType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VModelPhaseValidations_VModelPhaseId_ValidatedAt",
                table: "VModelPhaseValidations",
                columns: new[] { "VModelPhaseId", "ValidatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VModelTestCaseImplementationArtifacts_ImplementationArtifactId",
                table: "VModelTestCaseImplementationArtifacts",
                column: "ImplementationArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_VModelTestCases_ProjectId_Identifier",
                table: "VModelTestCases",
                columns: new[] { "ProjectId", "Identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VModelTestCases_ProjectId_TestLevel",
                table: "VModelTestCases",
                columns: new[] { "ProjectId", "TestLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_VModelTestExecutionIssues_IssueId",
                table: "VModelTestExecutionIssues",
                column: "IssueId");

            migrationBuilder.CreateIndex(
                name: "IX_VModelTestExecutions_VModelTestCaseId_ExecutedAt",
                table: "VModelTestExecutions",
                columns: new[] { "VModelTestCaseId", "ExecutedAt" });

            migrationBuilder.Sql(
                """
                INSERT INTO [VModelPhases]
                    ([ProjectId], [PhaseType], [PhaseOrder], [Status], [StartedAt], [CompletedAt], [CreatedAt], [UpdatedAt])
                SELECT p.[Id], configured.[PhaseType], configured.[PhaseOrder], 1, NULL, NULL, SYSUTCDATETIME(), NULL
                FROM [Projects] AS p
                CROSS JOIN (VALUES
                    (1, 1), (2, 2), (3, 3), (4, 4),
                    (5, 5), (6, 6), (7, 7), (8, 8)
                ) AS configured([PhaseType], [PhaseOrder])
                WHERE p.[Methodology] = 2
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [VModelPhases] AS existing
                      WHERE existing.[ProjectId] = p.[Id]
                        AND existing.[PhaseType] = configured.[PhaseType]
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VModelPhaseValidations");

            migrationBuilder.DropTable(
                name: "VModelTestCaseImplementationArtifacts");

            migrationBuilder.DropTable(
                name: "VModelTestExecutionIssues");

            migrationBuilder.DropTable(
                name: "VModelPhases");

            migrationBuilder.DropTable(
                name: "VModelTestExecutions");

            migrationBuilder.DropTable(
                name: "VModelTestCases");
        }
    }
}
