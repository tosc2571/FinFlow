using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameCategorySortOrderToPriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SortOrder",
                table: "Categories",
                newName: "Priority");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Priority",
                table: "Categories",
                newName: "SortOrder");
        }
    }
}
