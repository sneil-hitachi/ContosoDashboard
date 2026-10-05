using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContosoDashboard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskDocumentsAndActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentActivities",
                columns: table => new
                {
                    DocumentActivityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: true),
                    DocumentTitleSnapshot = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ActorUserId = table.Column<int>(type: "int", nullable: true),
                    ActorNameSnapshot = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentActivities", x => x.DocumentActivityId);
                });

            migrationBuilder.CreateTable(
                name: "TaskDocuments",
                columns: table => new
                {
                    TaskDocumentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    AttachedByUserId = table.Column<int>(type: "int", nullable: false),
                    AttachedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskDocuments", x => x.TaskDocumentId);
                    table.ForeignKey(
                        name: "FK_TaskDocuments_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskDocuments_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "TaskId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskDocuments_Users_AttachedByUserId",
                        column: x => x.AttachedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentActivities_Action_OccurredAtUtc",
                table: "DocumentActivities",
                columns: new[] { "Action", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentActivities_ActorUserId_OccurredAtUtc",
                table: "DocumentActivities",
                columns: new[] { "ActorUserId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentActivities_DocumentId_OccurredAtUtc",
                table: "DocumentActivities",
                columns: new[] { "DocumentId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskDocuments_AttachedByUserId",
                table: "TaskDocuments",
                column: "AttachedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDocuments_DocumentId",
                table: "TaskDocuments",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDocuments_TaskId_DocumentId",
                table: "TaskDocuments",
                columns: new[] { "TaskId", "DocumentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentActivities");

            migrationBuilder.DropTable(
                name: "TaskDocuments");
        }
    }
}
