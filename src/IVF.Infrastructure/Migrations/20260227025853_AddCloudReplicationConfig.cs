using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCloudReplicationConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CloudReplicationConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DbReplicationEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RemoteDbHost = table.Column<string>(type: "text", nullable: true),
                    RemoteDbPort = table.Column<int>(type: "integer", nullable: false),
                    RemoteDbUser = table.Column<string>(type: "text", nullable: true),
                    RemoteDbPassword = table.Column<string>(type: "text", nullable: true),
                    RemoteDbSslMode = table.Column<string>(type: "text", nullable: false),
                    RemoteDbSlotName = table.Column<string>(type: "text", nullable: true),
                    RemoteDbAllowedIps = table.Column<string>(type: "text", nullable: true),
                    MinioReplicationEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RemoteMinioEndpoint = table.Column<string>(type: "text", nullable: true),
                    RemoteMinioAccessKey = table.Column<string>(type: "text", nullable: true),
                    RemoteMinioSecretKey = table.Column<string>(type: "text", nullable: true),
                    RemoteMinioBucket = table.Column<string>(type: "text", nullable: false),
                    RemoteMinioUseSsl = table.Column<bool>(type: "boolean", nullable: false),
                    RemoteMinioRegion = table.Column<string>(type: "text", nullable: false),
                    RemoteMinioSyncMode = table.Column<string>(type: "text", nullable: false),
                    RemoteMinioSyncCron = table.Column<string>(type: "text", nullable: true),
                    LastDbSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastMinioSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastDbSyncStatus = table.Column<string>(type: "text", nullable: true),
                    LastMinioSyncStatus = table.Column<string>(type: "text", nullable: true),
                    LastMinioSyncBytes = table.Column<long>(type: "bigint", nullable: false),
                    LastMinioSyncFiles = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CloudReplicationConfigs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CloudReplicationConfigs");
        }
    }
}
