using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kyc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCase : Migration
    {
        private static readonly string[] CasesTenantIdCustomerUserIdColumns = ["TenantId", "CustomerUserId"];
        private static readonly string[] CasesTenantIdStatusColumns = ["TenantId", "Status"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FormData = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cases_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cases_users_CustomerUserId",
                        column: x => x.CustomerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cases_users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cases_CustomerUserId",
                table: "cases",
                column: "CustomerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_cases_ReviewedBy",
                table: "cases",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_cases_TenantId_CustomerUserId",
                table: "cases",
                columns: CasesTenantIdCustomerUserIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_cases_TenantId_Status",
                table: "cases",
                columns: CasesTenantIdStatusColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cases");
        }
    }
}
