using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomDomainVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomDomainStatus",
                table: "tenants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "CustomDomainVerificationToken",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CustomDomainVerifiedAt",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_CustomDomain",
                table: "tenants",
                column: "CustomDomain",
                unique: true,
                filter: "\"CustomDomain\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tenants_CustomDomain",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "CustomDomainStatus",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "CustomDomainVerificationToken",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "CustomDomainVerifiedAt",
                table: "tenants");
        }
    }
}
