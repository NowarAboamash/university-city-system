using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HousingService.Migrations
{
    /// <inheritdoc />
    public partial class FixGroupInvitationUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GroupInvitations_HousingGroupId_InvitedStudentId",
                table: "GroupInvitations");

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvitations_HousingGroupId_InvitedStudentId",
                table: "GroupInvitations",
                columns: new[] { "HousingGroupId", "InvitedStudentId" },
                unique: true,
                filter: "[Status] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GroupInvitations_HousingGroupId_InvitedStudentId",
                table: "GroupInvitations");

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvitations_HousingGroupId_InvitedStudentId",
                table: "GroupInvitations",
                columns: new[] { "HousingGroupId", "InvitedStudentId" },
                unique: true);
        }
    }
}
