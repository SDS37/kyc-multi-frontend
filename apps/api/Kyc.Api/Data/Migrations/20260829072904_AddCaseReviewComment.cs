using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kyc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseReviewComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewComment",
                table: "cases",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewComment",
                table: "cases");
        }
    }
}
