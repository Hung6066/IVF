using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentSignaturesAndPatientDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_signatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FormResponseId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SignatureRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_signatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_signatures_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "patient_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: true),
                    FormResponseId = table.Column<Guid>(type: "uuid", nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Confidentiality = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BucketName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsSigned = table.Column<bool>(type: "boolean", nullable: false),
                    SignedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SignedObjectKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    PreviousVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Tags = table.Column<string>(type: "jsonb", nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_patient_documents_patient_documents_PreviousVersionId",
                        column: x => x.PreviousVersionId,
                        principalTable: "patient_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_patient_documents_patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_signatures_FormResponseId",
                table: "document_signatures",
                column: "FormResponseId");

            migrationBuilder.CreateIndex(
                name: "IX_document_signatures_FormResponseId_UserId_SignatureRole",
                table: "document_signatures",
                columns: new[] { "FormResponseId", "UserId", "SignatureRole" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_document_signatures_UserId",
                table: "document_signatures",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_patient_documents_BucketName_ObjectKey",
                table: "patient_documents",
                columns: new[] { "BucketName", "ObjectKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_patient_documents_CreatedAt",
                table: "patient_documents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_patient_documents_DocumentType",
                table: "patient_documents",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_patient_documents_IsSigned",
                table: "patient_documents",
                column: "IsSigned");

            migrationBuilder.CreateIndex(
                name: "IX_patient_documents_PatientId",
                table: "patient_documents",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_patient_documents_PatientId_DocumentType_Status",
                table: "patient_documents",
                columns: new[] { "PatientId", "DocumentType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_patient_documents_PreviousVersionId",
                table: "patient_documents",
                column: "PreviousVersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_signatures");

            migrationBuilder.DropTable(
                name: "patient_documents");
        }
    }
}
