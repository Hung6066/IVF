using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorTenantSubCaToEjbca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tenant_sub_cas_CertificateAuthorities_CertificateAuthorityId",
                table: "tenant_sub_cas");

            migrationBuilder.DropIndex(
                name: "IX_tenant_sub_cas_CertificateAuthorityId",
                table: "tenant_sub_cas");

            migrationBuilder.DropColumn(
                name: "CertificateAuthorityId",
                table: "tenant_sub_cas");

            migrationBuilder.AddColumn<string>(
                name: "EjbcaCaName",
                table: "tenant_sub_cas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EjbcaCertProfileName",
                table: "tenant_sub_cas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EjbcaEeProfileName",
                table: "tenant_sub_cas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EjbcaCaName",
                table: "tenant_sub_cas");

            migrationBuilder.DropColumn(
                name: "EjbcaCertProfileName",
                table: "tenant_sub_cas");

            migrationBuilder.DropColumn(
                name: "EjbcaEeProfileName",
                table: "tenant_sub_cas");

            migrationBuilder.AddColumn<Guid>(
                name: "CertificateAuthorityId",
                table: "tenant_sub_cas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_tenant_sub_cas_CertificateAuthorityId",
                table: "tenant_sub_cas",
                column: "CertificateAuthorityId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_sub_cas_CertificateAuthorities_CertificateAuthorityId",
                table: "tenant_sub_cas",
                column: "CertificateAuthorityId",
                principalTable: "CertificateAuthorities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
