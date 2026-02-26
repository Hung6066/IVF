using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCloudBackupColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CloudSyncEnabled",
                table: "backup_schedule_config",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CloudStorageKey",
                table: "backup_operations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CloudUploadedAt",
                table: "backup_operations",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CloudSyncEnabled",
                table: "backup_schedule_config");

            migrationBuilder.DropColumn(
                name: "CloudStorageKey",
                table: "backup_operations");

            migrationBuilder.DropColumn(
                name: "CloudUploadedAt",
                table: "backup_operations");
        }
    }
}
