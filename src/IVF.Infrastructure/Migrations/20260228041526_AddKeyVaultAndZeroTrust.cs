using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKeyVaultAndZeroTrust : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "api_key_management",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    KeyHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Environment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RotationIntervalDays = table.Column<int>(type: "integer", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRotatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_key_management", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "device_risks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RiskLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RiskScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Factors = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsTrusted = table.Column<bool>(type: "boolean", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Country = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_risks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "zt_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequiredAuthLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MaxAllowedRisk = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RequireTrustedDevice = table.Column<bool>(type: "boolean", nullable: false),
                    RequireFreshSession = table.Column<bool>(type: "boolean", nullable: false),
                    BlockAnomaly = table.Column<bool>(type: "boolean", nullable: false),
                    RequireGeoFence = table.Column<bool>(type: "boolean", nullable: false),
                    AllowedCountries = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BlockVpnTor = table.Column<bool>(type: "boolean", nullable: false),
                    AllowBreakGlassOverride = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zt_policies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_key_management_ExpiresAt",
                table: "api_key_management",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_api_key_management_IsActive",
                table: "api_key_management",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_api_key_management_ServiceName_KeyName",
                table: "api_key_management",
                columns: new[] { "ServiceName", "KeyName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_risks_RiskLevel",
                table: "device_risks",
                column: "RiskLevel");

            migrationBuilder.CreateIndex(
                name: "IX_device_risks_UserId_DeviceId",
                table: "device_risks",
                columns: new[] { "UserId", "DeviceId" });

            migrationBuilder.CreateIndex(
                name: "IX_zt_policies_Action",
                table: "zt_policies",
                column: "Action",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_zt_policies_IsActive",
                table: "zt_policies",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_key_management");

            migrationBuilder.DropTable(
                name: "device_risks");

            migrationBuilder.DropTable(
                name: "zt_policies");
        }
    }
}
