using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvertisingService.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Advertisements",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Type = table.Column<int>(type: "int", nullable: false),
                TargetGender = table.Column<int>(type: "int", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                Priority = table.Column<int>(type: "int", nullable: false),
                StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Advertisements", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AdvertisementColleges",
            columns: table => new
            {
                AdvertisementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CollegeId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AdvertisementColleges", x => new { x.AdvertisementId, x.CollegeId });
                table.ForeignKey(
                    name: "FK_AdvertisementColleges_Advertisements_AdvertisementId",
                    column: x => x.AdvertisementId,
                    principalTable: "Advertisements",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AdvertisementGovernorates",
            columns: table => new
            {
                AdvertisementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GovernorateId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AdvertisementGovernorates", x => new { x.AdvertisementId, x.GovernorateId });
                table.ForeignKey(
                    name: "FK_AdvertisementGovernorates_Advertisements_AdvertisementId",
                    column: x => x.AdvertisementId,
                    principalTable: "Advertisements",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Advertisements_EndDate",
            table: "Advertisements",
            column: "EndDate");

        migrationBuilder.CreateIndex(
            name: "IX_Advertisements_IsActive",
            table: "Advertisements",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_Advertisements_StartDate",
            table: "Advertisements",
            column: "StartDate");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AdvertisementColleges");

        migrationBuilder.DropTable(
            name: "AdvertisementGovernorates");

        migrationBuilder.DropTable(
            name: "Advertisements");
    }
}
