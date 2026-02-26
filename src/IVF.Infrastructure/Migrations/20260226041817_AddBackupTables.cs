using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBackupTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "backup_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StartedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LogLinesJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_operations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "backup_schedule_config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CronExpression = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    KeysOnly = table.Column<bool>(type: "boolean", nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false),
                    MaxBackupCount = table.Column<int>(type: "integer", nullable: false),
                    LastScheduledRun = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastScheduledOperationCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_schedule_config", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_backup_operations_OperationCode",
                table: "backup_operations",
                column: "OperationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_backup_operations_StartedAt",
                table: "backup_operations",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_backup_operations_Status",
                table: "backup_operations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_backup_operations_Type",
                table: "backup_operations",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "backup_operations");

            migrationBuilder.DropTable(
                name: "backup_schedule_config");
        }
    }
}
