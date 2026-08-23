using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HousingService.Migrations
{
    /// <inheritdoc />
    public partial class AddHousingCycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HousingCycles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HousingCycles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HousingCycles_Name",
                table: "HousingCycles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HousingCycles_Status",
                table: "HousingCycles",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HousingCycles");
        }
    }
}
