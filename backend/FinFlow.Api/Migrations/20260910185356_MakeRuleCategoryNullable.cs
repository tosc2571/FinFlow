using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakeRuleCategoryNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClassificationRules_Categories_CategoryId",
                table: "ClassificationRules");

            migrationBuilder.AlterColumn<int>(
                name: "CategoryId",
                table: "ClassificationRules",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddForeignKey(
                name: "FK_ClassificationRules_Categories_CategoryId",
                table: "ClassificationRules",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClassificationRules_Categories_CategoryId",
                table: "ClassificationRules");

            migrationBuilder.AlterColumn<int>(
                name: "CategoryId",
                table: "ClassificationRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ClassificationRules_Categories_CategoryId",
                table: "ClassificationRules",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
