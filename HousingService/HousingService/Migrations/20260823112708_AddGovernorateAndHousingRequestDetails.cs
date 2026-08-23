using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HousingService.Migrations
{
    /// <inheritdoc />
    public partial class AddGovernorateAndHousingRequestDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AcademicLevel",
                table: "HousingRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DetailedAddress",
                table: "HousingRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "GovernorateId",
                table: "HousingRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "HasSpecialNeeds",
                table: "HousingRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "HousingCycleId",
                table: "HousingRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsPreviousResident",
                table: "HousingRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PreviousBuildingId",
                table: "HousingRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreviousFloor",
                table: "HousingRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousRoomNumber",
                table: "HousingRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Governorates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Governorates", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Governorates",
                columns: new[] { "Id", "CreatedAt", "Name", "UpdatedAt" },
                values: new object[] { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "حلب", null });

            migrationBuilder.CreateIndex(
                name: "IX_HousingRequests_GovernorateId",
                table: "HousingRequests",
                column: "GovernorateId");

            migrationBuilder.CreateIndex(
                name: "IX_HousingRequests_HousingCycleId",
                table: "HousingRequests",
                column: "HousingCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_HousingRequests_PreviousBuildingId",
                table: "HousingRequests",
                column: "PreviousBuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_HousingRequests_StudentId_HousingCycleId",
                table: "HousingRequests",
                columns: new[] { "StudentId", "HousingCycleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Governorates_Name",
                table: "Governorates",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_HousingRequests_Buildings_PreviousBuildingId",
                table: "HousingRequests",
                column: "PreviousBuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HousingRequests_Governorates_GovernorateId",
                table: "HousingRequests",
                column: "GovernorateId",
                principalTable: "Governorates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HousingRequests_HousingCycles_HousingCycleId",
                table: "HousingRequests",
                column: "HousingCycleId",
                principalTable: "HousingCycles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HousingRequests_Buildings_PreviousBuildingId",
                table: "HousingRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_HousingRequests_Governorates_GovernorateId",
                table: "HousingRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_HousingRequests_HousingCycles_HousingCycleId",
                table: "HousingRequests");

            migrationBuilder.DropTable(
                name: "Governorates");

            migrationBuilder.DropIndex(
                name: "IX_HousingRequests_GovernorateId",
                table: "HousingRequests");

            migrationBuilder.DropIndex(
                name: "IX_HousingRequests_HousingCycleId",
                table: "HousingRequests");

            migrationBuilder.DropIndex(
                name: "IX_HousingRequests_PreviousBuildingId",
                table: "HousingRequests");

            migrationBuilder.DropIndex(
                name: "IX_HousingRequests_StudentId_HousingCycleId",
                table: "HousingRequests");

            migrationBuilder.DropColumn(
                name: "AcademicLevel",
                table: "HousingRequests");

            migrationBuilder.DropColumn(
                name: "DetailedAddress",
                table: "HousingRequests");

            migrationBuilder.DropColumn(
                name: "GovernorateId",
                table: "HousingRequests");

            migrationBuilder.DropColumn(
                name: "HasSpecialNeeds",
                table: "HousingRequests");

            migrationBuilder.DropColumn(
                name: "HousingCycleId",
                table: "HousingRequests");

            migrationBuilder.DropColumn(
                name: "IsPreviousResident",
                table: "HousingRequests");

            migrationBuilder.DropColumn(
                name: "PreviousBuildingId",
                table: "HousingRequests");

            migrationBuilder.DropColumn(
                name: "PreviousFloor",
                table: "HousingRequests");

            migrationBuilder.DropColumn(
                name: "PreviousRoomNumber",
                table: "HousingRequests");
        }
    }
}
