using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContosoDashboard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentShares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentShares",
                columns: table => new
                {
                    DocumentShareId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    GrantedByUserId = table.Column<int>(type: "int", nullable: false),
                    RecipientUserId = table.Column<int>(type: "int", nullable: true),
                    RecipientDepartment = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GrantedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentShares", x => x.DocumentShareId);
                    table.CheckConstraint("CK_DocumentShares_OneRecipient", "([RecipientUserId] IS NOT NULL AND [RecipientDepartment] IS NULL) OR ([RecipientUserId] IS NULL AND [RecipientDepartment] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_DocumentShares_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentShares_Users_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentShares_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShares_DocumentId_RecipientDepartment",
                table: "DocumentShares",
                columns: new[] { "DocumentId", "RecipientDepartment" },
                unique: true,
                filter: "[RecipientDepartment] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShares_DocumentId_RecipientUserId",
                table: "DocumentShares",
                columns: new[] { "DocumentId", "RecipientUserId" },
                unique: true,
                filter: "[RecipientUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShares_GrantedByUserId",
                table: "DocumentShares",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShares_RecipientUserId",
                table: "DocumentShares",
                column: "RecipientUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentShares");
        }
    }
}
