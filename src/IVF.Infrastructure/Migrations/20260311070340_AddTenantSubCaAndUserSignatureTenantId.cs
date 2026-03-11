using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSubCaAndUserSignatureTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "user_signatures",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "tenant_sub_cas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateAuthorityId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkerNamePrefix = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OrganizationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AutoProvisionEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultCertValidityDays = table.Column<int>(type: "integer", nullable: false),
                    RenewBeforeDays = table.Column<int>(type: "integer", nullable: false),
                    MaxWorkers = table.Column<int>(type: "integer", nullable: false),
                    ActiveWorkerCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_sub_cas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_sub_cas_CertificateAuthorities_CertificateAuthorityId",
                        column: x => x.CertificateAuthorityId,
                        principalTable: "CertificateAuthorities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tenant_sub_cas_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_signatures_TenantId",
                table: "user_signatures",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_sub_cas_CertificateAuthorityId",
                table: "tenant_sub_cas",
                column: "CertificateAuthorityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_sub_cas_TenantId",
                table: "tenant_sub_cas",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_sub_cas");

            migrationBuilder.DropIndex(
                name: "IX_user_signatures_TenantId",
                table: "user_signatures");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "user_signatures");
        }
    }
}
