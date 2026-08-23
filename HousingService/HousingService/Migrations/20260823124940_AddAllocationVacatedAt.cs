using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HousingService.Migrations
{
    /// <inheritdoc />
    public partial class AddAllocationVacatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "VacatedAt",
                table: "Allocations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_VacatedAt",
                table: "Allocations",
                column: "VacatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Allocations_VacatedAt",
                table: "Allocations");

            migrationBuilder.DropColumn(
                name: "VacatedAt",
                table: "Allocations");
        }
    }
}
