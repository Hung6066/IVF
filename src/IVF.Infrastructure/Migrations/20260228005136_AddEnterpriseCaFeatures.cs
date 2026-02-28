using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEnterpriseCaFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RevocationReason",
                table: "ManagedCertificates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAt",
                table: "ManagedCertificates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "NextCrlNumber",
                table: "CertificateAuthorities",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "CertificateAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateId = table.Column<Guid>(type: "uuid", nullable: true),
                    CaId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Actor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceIp = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CertificateRevocationLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CrlNumber = table.Column<long>(type: "bigint", nullable: false),
                    ThisUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CrlPem = table.Column<string>(type: "text", nullable: false),
                    CrlDer = table.Column<byte[]>(type: "bytea", nullable: false),
                    RevokedCount = table.Column<int>(type: "integer", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateRevocationLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificateRevocationLists_CertificateAuthorities_CaId",
                        column: x => x.CaId,
                        principalTable: "CertificateAuthorities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CertificateAuditEvents_CaId",
                table: "CertificateAuditEvents",
                column: "CaId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateAuditEvents_CertificateId",
                table: "CertificateAuditEvents",
                column: "CertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateAuditEvents_CreatedAt",
                table: "CertificateAuditEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateAuditEvents_EventType",
                table: "CertificateAuditEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateRevocationLists_CaId_CrlNumber",
                table: "CertificateRevocationLists",
                columns: new[] { "CaId", "CrlNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CertificateRevocationLists_NextUpdate",
                table: "CertificateRevocationLists",
                column: "NextUpdate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CertificateAuditEvents");

            migrationBuilder.DropTable(
                name: "CertificateRevocationLists");

            migrationBuilder.DropColumn(
                name: "RevocationReason",
                table: "ManagedCertificates");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "ManagedCertificates");

            migrationBuilder.DropColumn(
                name: "NextCrlNumber",
                table: "CertificateAuthorities");
        }
    }
}
