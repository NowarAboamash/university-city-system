using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeedbackService.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFeedbackImageUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE Name = N'ImageUrl'
      AND Object_ID = Object_ID(N'[FeedbackImages]')
)
BEGIN
    ALTER TABLE [FeedbackImages] DROP COLUMN [ImageUrl];
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE Name = N'ImageUrl'
      AND Object_ID = Object_ID(N'[FeedbackImages]')
)
BEGIN
    ALTER TABLE [FeedbackImages] ADD [ImageUrl] nvarchar(max) NULL;
END");
        }
    }
}
