using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVaultTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vault_audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResourceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Details = table.Column<string>(type: "jsonb", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vault_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vault_auto_unseal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WrappedKey = table.Column<string>(type: "text", nullable: false),
                    KeyVaultUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    KeyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    KeyVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Algorithm = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Iv = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vault_auto_unseal", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vault_dynamic_credentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Backend = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DbHost = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DbPort = table.Column<int>(type: "integer", nullable: false),
                    DbName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AdminUsername = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AdminPasswordEncrypted = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Revoked = table.Column<bool>(type: "boolean", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vault_dynamic_credentials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vault_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PathPattern = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Capabilities = table.Column<string[]>(type: "text[]", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vault_policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vault_secrets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    EncryptedData = table.Column<string>(type: "text", nullable: false),
                    Iv = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeaseId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LeaseTtl = table.Column<int>(type: "integer", nullable: true),
                    LeaseRenewable = table.Column<bool>(type: "boolean", nullable: false),
                    LeaseExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vault_secrets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vault_settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ValueJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vault_settings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "vault_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Accessor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Policies = table.Column<string[]>(type: "text[]", nullable: false),
                    TokenType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Ttl = table.Column<int>(type: "integer", nullable: true),
                    NumUses = table.Column<int>(type: "integer", nullable: true),
                    UsesCount = table.Column<int>(type: "integer", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Revoked = table.Column<bool>(type: "boolean", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vault_tokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vault_user_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GrantedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vault_user_policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vault_user_policies_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_vault_user_policies_vault_policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "vault_policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vault_leases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SecretId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ttl = table.Column<int>(type: "integer", nullable: false),
                    Renewable = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Revoked = table.Column<bool>(type: "boolean", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vault_leases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vault_leases_vault_secrets_SecretId",
                        column: x => x.SecretId,
                        principalTable: "vault_secrets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vault_audit_logs_Action",
                table: "vault_audit_logs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_vault_audit_logs_CreatedAt",
                table: "vault_audit_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_vault_audit_logs_UserId",
                table: "vault_audit_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_vault_dynamic_credentials_ExpiresAt",
                table: "vault_dynamic_credentials",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_vault_dynamic_credentials_LeaseId",
                table: "vault_dynamic_credentials",
                column: "LeaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vault_leases_ExpiresAt",
                table: "vault_leases",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_vault_leases_LeaseId",
                table: "vault_leases",
                column: "LeaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vault_leases_SecretId",
                table: "vault_leases",
                column: "SecretId");

            migrationBuilder.CreateIndex(
                name: "IX_vault_policies_Name",
                table: "vault_policies",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vault_secrets_LeaseId",
                table: "vault_secrets",
                column: "LeaseId",
                filter: "\"LeaseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_vault_secrets_Path",
                table: "vault_secrets",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_vault_secrets_Path_Version",
                table: "vault_secrets",
                columns: new[] { "Path", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vault_tokens_Accessor",
                table: "vault_tokens",
                column: "Accessor",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vault_tokens_ExpiresAt",
                table: "vault_tokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_vault_user_policies_PolicyId",
                table: "vault_user_policies",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_vault_user_policies_UserId_PolicyId",
                table: "vault_user_policies",
                columns: new[] { "UserId", "PolicyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vault_audit_logs");

            migrationBuilder.DropTable(
                name: "vault_auto_unseal");

            migrationBuilder.DropTable(
                name: "vault_dynamic_credentials");

            migrationBuilder.DropTable(
                name: "vault_leases");

            migrationBuilder.DropTable(
                name: "vault_settings");

            migrationBuilder.DropTable(
                name: "vault_tokens");

            migrationBuilder.DropTable(
                name: "vault_user_policies");

            migrationBuilder.DropTable(
                name: "vault_secrets");

            migrationBuilder.DropTable(
                name: "vault_policies");
        }
    }
}
