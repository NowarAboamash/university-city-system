using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeedbackService.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FeedbackImage_Feedback_FeedbackId",
                table: "FeedbackImage");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FeedbackImage",
                table: "FeedbackImage");

            migrationBuilder.RenameTable(
                name: "FeedbackImage",
                newName: "FeedbackImages");

            migrationBuilder.RenameIndex(
                name: "IX_FeedbackImage_FeedbackId",
                table: "FeedbackImages",
                newName: "IX_FeedbackImages_FeedbackId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FeedbackImages",
                table: "FeedbackImages",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FeedbackImages_Feedback_FeedbackId",
                table: "FeedbackImages",
                column: "FeedbackId",
                principalTable: "Feedback",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FeedbackImages_Feedback_FeedbackId",
                table: "FeedbackImages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FeedbackImages",
                table: "FeedbackImages");

            migrationBuilder.RenameTable(
                name: "FeedbackImages",
                newName: "FeedbackImage");

            migrationBuilder.RenameIndex(
                name: "IX_FeedbackImages_FeedbackId",
                table: "FeedbackImage",
                newName: "IX_FeedbackImage_FeedbackId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FeedbackImage",
                table: "FeedbackImage",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FeedbackImage_Feedback_FeedbackId",
                table: "FeedbackImage",
                column: "FeedbackId",
                principalTable: "Feedback",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
