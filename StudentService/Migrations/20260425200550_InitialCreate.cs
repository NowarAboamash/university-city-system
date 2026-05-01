using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentService.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Colleges",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Colleges", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Governorates",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Governorates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Students",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                UniversityId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                NationalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Nationality = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CollegeId = table.Column<int>(type: "int", nullable: false),
                GovernorateId = table.Column<int>(type: "int", nullable: false),
                Village = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Students", x => x.Id);
                table.ForeignKey(
                    name: "FK_Students_Colleges_CollegeId",
                    column: x => x.CollegeId,
                    principalTable: "Colleges",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Students_Governorates_GovernorateId",
                    column: x => x.GovernorateId,
                    principalTable: "Governorates",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Students_CollegeId",
            table: "Students",
            column: "CollegeId");

        migrationBuilder.CreateIndex(
            name: "IX_Students_GovernorateId",
            table: "Students",
            column: "GovernorateId");

        migrationBuilder.CreateIndex(
            name: "IX_Students_UserId",
            table: "Students",
            column: "UserId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Students");

        migrationBuilder.DropTable(
            name: "Colleges");

        migrationBuilder.DropTable(
            name: "Governorates");
    }
}
