using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCertDeploymentLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CertDeploymentLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Target = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Container = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RemoteHost = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LogLines = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertDeploymentLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertDeploymentLogs_ManagedCertificates_CertificateId",
                        column: x => x.CertificateId,
                        principalTable: "ManagedCertificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CertDeploymentLogs_CertificateId",
                table: "CertDeploymentLogs",
                column: "CertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_CertDeploymentLogs_OperationId",
                table: "CertDeploymentLogs",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CertDeploymentLogs_StartedAt",
                table: "CertDeploymentLogs",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CertDeploymentLogs_Status",
                table: "CertDeploymentLogs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CertDeploymentLogs");
        }
    }
}
