using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HousingService.Migrations
{
    /// <inheritdoc />
    public partial class AllowGroupReallocationAfterVacate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Allocations_HousingGroupId",
                table: "Allocations");

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_HousingGroupId",
                table: "Allocations",
                column: "HousingGroupId",
                unique: true,
                filter: "[HousingGroupId] IS NOT NULL AND [VacatedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Allocations_HousingGroupId",
                table: "Allocations");

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_HousingGroupId",
                table: "Allocations",
                column: "HousingGroupId",
                unique: true,
                filter: "[HousingGroupId] IS NOT NULL");
        }
    }
}
