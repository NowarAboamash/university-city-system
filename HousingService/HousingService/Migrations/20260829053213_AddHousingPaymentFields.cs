using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HousingService.Migrations
{
    /// <inheritdoc />
    public partial class AddHousingPaymentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPaid",
                table: "HousingRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAt",
                table: "HousingRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentDueDate",
                table: "HousingRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReminderSent",
                table: "HousingRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "HousingSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentDeadlineDays = table.Column<int>(type: "int", nullable: false),
                    ReminderDaysBefore = table.Column<int>(type: "int", nullable: false),
                    HousingFeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HousingSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "HousingSettings",
                columns: new[] { "Id", "CreatedAt", "HousingFeeAmount", "PaymentDeadlineDays", "ReminderDaysBefore", "UpdatedAt" },
                values: new object[] { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0m, 15, 3, null });

            // Backfill requests that were already Accepted before this feature existed, so they
            // aren't silently exempt from the payment reminder. 15 == the default
            // PaymentDeadlineDays seeded just above; measured from acceptance (LockedAt) or,
            // failing that, creation. Runs on SQL Server only; a no-op when there are no such rows.
            migrationBuilder.Sql(@"
UPDATE hr
SET hr.PaymentDueDate = DATEADD(day, 15, COALESCE(hr.LockedAt, hr.CreatedAt))
FROM HousingRequests hr
INNER JOIN AdmissionDecisions ad ON ad.HousingRequestId = hr.Id
WHERE ad.Status = 1 AND hr.IsPaid = 0 AND hr.PaymentDueDate IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HousingSettings");

            migrationBuilder.DropColumn(
                name: "IsPaid",
                table: "HousingRequests");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "HousingRequests");

            migrationBuilder.DropColumn(
                name: "PaymentDueDate",
                table: "HousingRequests");

            migrationBuilder.DropColumn(
                name: "ReminderSent",
                table: "HousingRequests");
        }
    }
}
