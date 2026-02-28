using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateAuthority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CertificateAuthorities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CommonName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Organization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OrganizationalUnit = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Country = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Locality = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    KeyAlgorithm = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    KeySize = table.Column<int>(type: "integer", nullable: false),
                    CertificatePem = table.Column<string>(type: "text", nullable: false),
                    PrivateKeyPem = table.Column<string>(type: "text", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NextSerialNumber = table.Column<long>(type: "bigint", nullable: false),
                    NotBefore = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NotAfter = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ParentCaId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChainPem = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateAuthorities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificateAuthorities_CertificateAuthorities_ParentCaId",
                        column: x => x.ParentCaId,
                        principalTable: "CertificateAuthorities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ManagedCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommonName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SubjectAltNames = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CertificatePem = table.Column<string>(type: "text", nullable: false),
                    PrivateKeyPem = table.Column<string>(type: "text", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NotBefore = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NotAfter = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    KeyAlgorithm = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    KeySize = table.Column<int>(type: "integer", nullable: false),
                    IssuingCaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DeployedTo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DeployedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RenewBeforeDays = table.Column<int>(type: "integer", nullable: false),
                    AutoRenewEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ReplacedCertId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReplacedByCertId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastRenewalAttempt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRenewalResult = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ValidityDays = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagedCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManagedCertificates_CertificateAuthorities_IssuingCaId",
                        column: x => x.IssuingCaId,
                        principalTable: "CertificateAuthorities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CertificateAuthorities_Fingerprint",
                table: "CertificateAuthorities",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CertificateAuthorities_Name",
                table: "CertificateAuthorities",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CertificateAuthorities_ParentCaId",
                table: "CertificateAuthorities",
                column: "ParentCaId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedCertificates_Fingerprint",
                table: "ManagedCertificates",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManagedCertificates_IssuingCaId",
                table: "ManagedCertificates",
                column: "IssuingCaId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedCertificates_Purpose",
                table: "ManagedCertificates",
                column: "Purpose");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedCertificates_Status",
                table: "ManagedCertificates",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManagedCertificates");

            migrationBuilder.DropTable(
                name: "CertificateAuthorities");
        }
    }
}
