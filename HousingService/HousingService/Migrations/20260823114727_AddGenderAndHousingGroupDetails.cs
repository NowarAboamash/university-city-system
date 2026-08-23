using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HousingService.Migrations
{
    /// <inheritdoc />
    public partial class AddGenderAndHousingGroupDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroupScore",
                table: "HousingGroups");

            migrationBuilder.AddColumn<int>(
                name: "Gender",
                table: "HousingRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "HousingGroups",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "HousingCycleId",
                table: "HousingGroups",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_HousingGroups_Code",
                table: "HousingGroups",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HousingGroups_HousingCycleId",
                table: "HousingGroups",
                column: "HousingCycleId");

            migrationBuilder.AddForeignKey(
                name: "FK_HousingGroups_HousingCycles_HousingCycleId",
                table: "HousingGroups",
                column: "HousingCycleId",
                principalTable: "HousingCycles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HousingGroups_HousingCycles_HousingCycleId",
                table: "HousingGroups");

            migrationBuilder.DropIndex(
                name: "IX_HousingGroups_Code",
                table: "HousingGroups");

            migrationBuilder.DropIndex(
                name: "IX_HousingGroups_HousingCycleId",
                table: "HousingGroups");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "HousingRequests");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "HousingGroups");

            migrationBuilder.DropColumn(
                name: "HousingCycleId",
                table: "HousingGroups");

            migrationBuilder.AddColumn<decimal>(
                name: "GroupScore",
                table: "HousingGroups",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
