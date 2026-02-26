using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCloudBackupConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cloud_backup_config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CompressionEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    S3Region = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    S3BucketName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    S3AccessKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    S3SecretKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    S3ServiceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    S3ForcePathStyle = table.Column<bool>(type: "boolean", nullable: false),
                    AzureConnectionString = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AzureContainerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GcsProjectId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GcsBucketName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GcsCredentialsPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cloud_backup_config", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cloud_backup_config");
        }
    }
}
