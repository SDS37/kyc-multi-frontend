using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kyc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocument : Migration
    {
        private static readonly string[] DocumentsTenantIdCaseIdColumns = ["TenantId", "CaseId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_documents_cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documents_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documents_users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_documents_CaseId",
                table: "documents",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_StorageKey",
                table: "documents",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documents_TenantId_CaseId",
                table: "documents",
                columns: DocumentsTenantIdCaseIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_documents_UploadedByUserId",
                table: "documents",
                column: "UploadedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "documents");
        }
    }
}
