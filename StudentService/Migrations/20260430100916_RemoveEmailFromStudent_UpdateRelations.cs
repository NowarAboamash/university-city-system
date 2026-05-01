using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentService.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEmailFromStudent_UpdateRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        migrationBuilder.DropColumn(
            name: "Email",
            table: "Students");

        migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Colleges_Name' AND object_id = OBJECT_ID(N'[Colleges]'))
BEGIN
    DROP INDEX [IX_Colleges_Name] ON [Colleges];
END

ALTER TABLE [Colleges] ALTER COLUMN [Name] nvarchar(450) NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Colleges_Name' AND object_id = OBJECT_ID(N'[Colleges]'))
BEGIN
    CREATE UNIQUE INDEX [IX_Colleges_Name] ON [Colleges] ([Name]);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Colleges_Name' AND object_id = OBJECT_ID(N'[Colleges]'))
BEGIN
    DROP INDEX [IX_Colleges_Name] ON [Colleges];
END

ALTER TABLE [Colleges] ALTER COLUMN [Name] nvarchar(max) NOT NULL;
");

        migrationBuilder.AddColumn<string>(
            name: "Email",
            table: "Students",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "");
        }
    }
}
