using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSignedDocumentAmendments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "signed_document_amendments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FormResponseId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReviewNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OldValuesSnapshot = table.Column<string>(type: "jsonb", nullable: true),
                    NewValuesSnapshot = table.Column<string>(type: "jsonb", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signed_document_amendments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_signed_document_amendments_users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_signed_document_amendments_users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "amendment_field_changes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AmendmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FormFieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FieldLabel = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ChangeType = table.Column<int>(type: "integer", nullable: false),
                    OldTextValue = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    OldNumericValue = table.Column<decimal>(type: "numeric", nullable: true),
                    OldDateValue = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OldBooleanValue = table.Column<bool>(type: "boolean", nullable: true),
                    OldJsonValue = table.Column<string>(type: "jsonb", nullable: true),
                    NewTextValue = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    NewNumericValue = table.Column<decimal>(type: "numeric", nullable: true),
                    NewDateValue = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NewBooleanValue = table.Column<bool>(type: "boolean", nullable: true),
                    NewJsonValue = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_amendment_field_changes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_amendment_field_changes_signed_document_amendments_Amendmen~",
                        column: x => x.AmendmentId,
                        principalTable: "signed_document_amendments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_amendment_field_changes_AmendmentId",
                table: "amendment_field_changes",
                column: "AmendmentId");

            migrationBuilder.CreateIndex(
                name: "IX_signed_document_amendments_FormResponseId",
                table: "signed_document_amendments",
                column: "FormResponseId");

            migrationBuilder.CreateIndex(
                name: "IX_signed_document_amendments_FormResponseId_Version",
                table: "signed_document_amendments",
                columns: new[] { "FormResponseId", "Version" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_signed_document_amendments_RequestedByUserId",
                table: "signed_document_amendments",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_signed_document_amendments_ReviewedByUserId",
                table: "signed_document_amendments",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_signed_document_amendments_Status",
                table: "signed_document_amendments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_signed_document_amendments_TenantId",
                table: "signed_document_amendments",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "amendment_field_changes");

            migrationBuilder.DropTable(
                name: "signed_document_amendments");
        }
    }
}
