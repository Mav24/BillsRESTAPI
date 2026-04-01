using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddBillSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BillShares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BillId = table.Column<int>(type: "INTEGER", nullable: false),
                    SharedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    SharedWithUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    SharedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillShares_Bills_BillId",
                        column: x => x.BillId,
                        principalTable: "Bills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BillShares_Users_SharedByUserId",
                        column: x => x.SharedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BillShares_Users_SharedWithUserId",
                        column: x => x.SharedWithUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillShares_BillId_SharedWithUserId",
                table: "BillShares",
                columns: new[] { "BillId", "SharedWithUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillShares_SharedByUserId",
                table: "BillShares",
                column: "SharedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BillShares_SharedWithUserId",
                table: "BillShares",
                column: "SharedWithUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillShares");
        }
    }
}
