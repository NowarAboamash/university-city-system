using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HousingService.Migrations
{
    /// <inheritdoc />
    public partial class AddHousingRequestFeeAmounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaid",
                table: "HousingRequests",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FeeAmount",
                table: "HousingRequests",
                type: "decimal(18,2)",
                nullable: true);

            // Backfill so the payment-summary aggregates are meaningful for pre-existing rows:
            // - every already-Accepted request gets FeeAmount = the current configured fee
            // - every already-paid request gets AmountPaid = its (now-set) FeeAmount
            // Runs on SQL Server only; a no-op when there are no such rows.
            migrationBuilder.Sql(@"
DECLARE @fee decimal(18,2) = (SELECT TOP 1 HousingFeeAmount FROM HousingSettings ORDER BY Id);
UPDATE hr SET hr.FeeAmount = @fee
FROM HousingRequests hr
INNER JOIN AdmissionDecisions ad ON ad.HousingRequestId = hr.Id
WHERE ad.Status = 1 AND hr.FeeAmount IS NULL;
UPDATE HousingRequests SET AmountPaid = COALESCE(FeeAmount, @fee)
WHERE IsPaid = 1 AND AmountPaid IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountPaid",
                table: "HousingRequests");

            migrationBuilder.DropColumn(
                name: "FeeAmount",
                table: "HousingRequests");
        }
    }
}
