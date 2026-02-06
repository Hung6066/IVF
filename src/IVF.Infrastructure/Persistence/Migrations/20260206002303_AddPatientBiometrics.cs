using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientBiometrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Fingerprint",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "Photo",
                table: "patients");

            migrationBuilder.CreateTable(
                name: "PatientFingerprints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    FingerprintData = table.Column<byte[]>(type: "bytea", nullable: false),
                    FingerType = table.Column<int>(type: "integer", nullable: false),
                    SdkType = table.Column<int>(type: "integer", nullable: false),
                    Quality = table.Column<int>(type: "integer", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientFingerprints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientFingerprints_patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatientPhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotoData = table.Column<byte[]>(type: "bytea", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientPhotos_patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientFingerprints_PatientId_FingerType",
                table: "PatientFingerprints",
                columns: new[] { "PatientId", "FingerType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientPhotos_PatientId",
                table: "PatientPhotos",
                column: "PatientId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientFingerprints");

            migrationBuilder.DropTable(
                name: "PatientPhotos");

            migrationBuilder.AddColumn<byte[]>(
                name: "Fingerprint",
                table: "patients",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Photo",
                table: "patients",
                type: "bytea",
                nullable: true);
        }
    }
}
