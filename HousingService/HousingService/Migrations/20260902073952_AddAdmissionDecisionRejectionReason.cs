using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HousingService.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionDecisionRejectionReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RejectionReason",
                table: "AdmissionDecisions",
                type: "int",
                nullable: true);

            // Existing rejections predate this column and were all manual review decisions —
            // stamp them AdminReview (0) so only genuine non-payment evictions read as NonPayment.
            migrationBuilder.Sql(
                "UPDATE AdmissionDecisions SET RejectionReason = 0 WHERE Status = 3 AND RejectionReason IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "AdmissionDecisions");
        }
    }
}
