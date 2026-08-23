using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HousingService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Buildings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Gender = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FloorsCount = table.Column<int>(type: "int", nullable: true),
                    StandardRoomCapacity = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Buildings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HousingGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeaderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    GroupScore = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    MaxMembers = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LockedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AllocatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HousingGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BuildingId = table.Column<int>(type: "int", nullable: false),
                    RoomNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Floor = table.Column<int>(type: "int", nullable: false),
                    CurrentOccupancy = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rooms_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupInvitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HousingGroupId = table.Column<int>(type: "int", nullable: false),
                    InvitedStudentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InvitedByStudentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupInvitations_HousingGroups_HousingGroupId",
                        column: x => x.HousingGroupId,
                        principalTable: "HousingGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HousingRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HousingGroupId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SpecialNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LockedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HousingRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HousingRequests_HousingGroups_HousingGroupId",
                        column: x => x.HousingGroupId,
                        principalTable: "HousingGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AdmissionDecisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HousingRequestId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DecisionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdmissionDecisions_HousingRequests_HousingRequestId",
                        column: x => x.HousingRequestId,
                        principalTable: "HousingRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Allocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HousingRequestId = table.Column<int>(type: "int", nullable: true),
                    HousingGroupId = table.Column<int>(type: "int", nullable: true),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    AllocatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Allocations_HousingGroups_HousingGroupId",
                        column: x => x.HousingGroupId,
                        principalTable: "HousingGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Allocations_HousingRequests_HousingRequestId",
                        column: x => x.HousingRequestId,
                        principalTable: "HousingRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Allocations_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HousingRequestDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HousingRequestId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    DocumentPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReviewStatus = table.Column<int>(type: "int", nullable: false),
                    ReviewNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HousingRequestDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HousingRequestDocuments_HousingRequests_HousingRequestId",
                        column: x => x.HousingRequestId,
                        principalTable: "HousingRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionDecisions_DecisionDate",
                table: "AdmissionDecisions",
                column: "DecisionDate");

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionDecisions_HousingRequestId",
                table: "AdmissionDecisions",
                column: "HousingRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionDecisions_Status",
                table: "AdmissionDecisions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_AllocatedAt",
                table: "Allocations",
                column: "AllocatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_HousingGroupId",
                table: "Allocations",
                column: "HousingGroupId",
                unique: true,
                filter: "[HousingGroupId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_HousingRequestId",
                table: "Allocations",
                column: "HousingRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_RoomId",
                table: "Allocations",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_Name",
                table: "Buildings",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvitations_HousingGroupId_InvitedStudentId",
                table: "GroupInvitations",
                columns: new[] { "HousingGroupId", "InvitedStudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvitations_InvitedStudentId",
                table: "GroupInvitations",
                column: "InvitedStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvitations_Status",
                table: "GroupInvitations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_HousingGroups_CreatedAt",
                table: "HousingGroups",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_HousingGroups_LeaderId",
                table: "HousingGroups",
                column: "LeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_HousingGroups_Status",
                table: "HousingGroups",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_HousingRequestDocuments_HousingRequestId",
                table: "HousingRequestDocuments",
                column: "HousingRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_HousingRequestDocuments_ReviewStatus",
                table: "HousingRequestDocuments",
                column: "ReviewStatus");

            migrationBuilder.CreateIndex(
                name: "IX_HousingRequests_HousingGroupId",
                table: "HousingRequests",
                column: "HousingGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_HousingRequests_Status",
                table: "HousingRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_HousingRequests_StudentId",
                table: "HousingRequests",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_HousingRequests_SubmittedAt",
                table: "HousingRequests",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_BuildingId_RoomNumber",
                table: "Rooms",
                columns: new[] { "BuildingId", "RoomNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdmissionDecisions");

            migrationBuilder.DropTable(
                name: "Allocations");

            migrationBuilder.DropTable(
                name: "GroupInvitations");

            migrationBuilder.DropTable(
                name: "HousingRequestDocuments");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "HousingRequests");

            migrationBuilder.DropTable(
                name: "Buildings");

            migrationBuilder.DropTable(
                name: "HousingGroups");
        }
    }
}
