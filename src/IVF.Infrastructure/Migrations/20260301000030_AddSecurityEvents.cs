using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "secret_rotation_schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SecretPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RotationIntervalDays = table.Column<int>(type: "integer", nullable: false),
                    GracePeriodHours = table.Column<int>(type: "integer", nullable: false),
                    AutomaticallyRotate = table.Column<bool>(type: "boolean", nullable: false),
                    RotationStrategy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "generate"),
                    CallbackUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LastRotatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextRotationAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_secret_rotation_schedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "security_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeviceFingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Country = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RequestPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequestMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: true),
                    Details = table.Column<string>(type: "jsonb", nullable: true),
                    ThreatIndicators = table.Column<string>(type: "jsonb", nullable: true),
                    RiskScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    IsBlocked = table.Column<bool>(type: "boolean", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_secret_rotation_schedules_IsActive_NextRotationAt",
                table: "secret_rotation_schedules",
                columns: new[] { "IsActive", "NextRotationAt" });

            migrationBuilder.CreateIndex(
                name: "IX_secret_rotation_schedules_SecretPath",
                table: "secret_rotation_schedules",
                column: "SecretPath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_security_events_CorrelationId",
                table: "security_events",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_security_events_CreatedAt",
                table: "security_events",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_security_events_EventType",
                table: "security_events",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_security_events_EventType_CreatedAt",
                table: "security_events",
                columns: new[] { "EventType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_security_events_IpAddress",
                table: "security_events",
                column: "IpAddress");

            migrationBuilder.CreateIndex(
                name: "IX_security_events_Severity",
                table: "security_events",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_security_events_Severity_CreatedAt",
                table: "security_events",
                columns: new[] { "Severity", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_security_events_UserId",
                table: "security_events",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_security_events_UserId_CreatedAt",
                table: "security_events",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "secret_rotation_schedules");

            migrationBuilder.DropTable(
                name: "security_events");
        }
    }
}
