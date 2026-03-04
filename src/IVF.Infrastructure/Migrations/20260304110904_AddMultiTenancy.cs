using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPlatformAdmin",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "treatment_cycles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "service_catalogs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "queue_tickets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "patients",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "notifications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "invoices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "form_templates",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "form_responses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "doctors",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "couples",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "appointments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TaxId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Website = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MaxUsers = table.Column<int>(type: "integer", nullable: false),
                    MaxPatientsPerMonth = table.Column<int>(type: "integer", nullable: false),
                    StorageLimitMb = table.Column<long>(type: "bigint", nullable: false),
                    AiEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DigitalSigningEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    BiometricsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AdvancedReportingEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ConnectionString = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DatabaseSchema = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: true),
                    PrimaryColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Locale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    TimeZone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CustomDomain = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            // Insert default tenant before FK constraints are applied
            var defaultTenantId = "00000000-0000-0000-0000-000000000001";
            var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.ffffff+00");
            migrationBuilder.Sql($@"
                INSERT INTO tenants (""Id"", ""Name"", ""Slug"", ""Status"", ""MaxUsers"", ""MaxPatientsPerMonth"", 
                    ""StorageLimitMb"", ""AiEnabled"", ""DigitalSigningEnabled"", ""BiometricsEnabled"", 
                    ""AdvancedReportingEnabled"", ""CreatedAt"", ""IsDeleted"")
                VALUES ('{defaultTenantId}', 'IVF Platform Default', 'default', 'Active', 100, 2000, 
                    102400, true, true, true, true, '{now}', false)
                ON CONFLICT DO NOTHING;
            ");

            // Update all existing rows to reference the default tenant
            var tables = new[] { "users", "patients", "doctors", "couples", "treatment_cycles",
                "queue_tickets", "invoices", "appointments", "notifications",
                "form_templates", "form_responses", "service_catalogs" };
            foreach (var table in tables)
            {
                migrationBuilder.Sql($@"UPDATE ""{table}"" SET ""TenantId"" = '{defaultTenantId}' WHERE ""TenantId"" = '00000000-0000-0000-0000-000000000000';");
            }

            migrationBuilder.CreateTable(
                name: "tenant_subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Plan = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BillingCycle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MonthlyPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TrialEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextBillingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AutoRenew = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_subscriptions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_usage_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    ActiveUsers = table.Column<int>(type: "integer", nullable: false),
                    NewPatients = table.Column<int>(type: "integer", nullable: false),
                    TreatmentCycles = table.Column<int>(type: "integer", nullable: false),
                    FormResponses = table.Column<int>(type: "integer", nullable: false),
                    SignedDocuments = table.Column<int>(type: "integer", nullable: false),
                    StorageUsedMb = table.Column<long>(type: "bigint", nullable: false),
                    ApiCalls = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_usage_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_usage_records_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_TenantId",
                table: "users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_treatment_cycles_TenantId",
                table: "treatment_cycles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_service_catalogs_TenantId",
                table: "service_catalogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_queue_tickets_TenantId",
                table: "queue_tickets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_patients_TenantId",
                table: "patients",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_TenantId",
                table: "notifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_TenantId",
                table: "invoices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_form_templates_TenantId",
                table: "form_templates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_form_responses_TenantId",
                table: "form_responses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_doctors_TenantId",
                table: "doctors",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_couples_TenantId",
                table: "couples",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_TenantId",
                table: "appointments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_subscriptions_TenantId",
                table: "tenant_subscriptions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_usage_records_TenantId_Year_Month",
                table: "tenant_usage_records",
                columns: new[] { "TenantId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Slug",
                table: "tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_users_tenants_TenantId",
                table: "users",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_tenants_TenantId",
                table: "users");

            migrationBuilder.DropTable(
                name: "tenant_subscriptions");

            migrationBuilder.DropTable(
                name: "tenant_usage_records");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_users_TenantId",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_treatment_cycles_TenantId",
                table: "treatment_cycles");

            migrationBuilder.DropIndex(
                name: "IX_service_catalogs_TenantId",
                table: "service_catalogs");

            migrationBuilder.DropIndex(
                name: "IX_queue_tickets_TenantId",
                table: "queue_tickets");

            migrationBuilder.DropIndex(
                name: "IX_patients_TenantId",
                table: "patients");

            migrationBuilder.DropIndex(
                name: "IX_notifications_TenantId",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_invoices_TenantId",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_form_templates_TenantId",
                table: "form_templates");

            migrationBuilder.DropIndex(
                name: "IX_form_responses_TenantId",
                table: "form_responses");

            migrationBuilder.DropIndex(
                name: "IX_doctors_TenantId",
                table: "doctors");

            migrationBuilder.DropIndex(
                name: "IX_couples_TenantId",
                table: "couples");

            migrationBuilder.DropIndex(
                name: "IX_appointments_TenantId",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "IsPlatformAdmin",
                table: "users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "treatment_cycles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "service_catalogs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "queue_tickets");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "form_templates");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "form_responses");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "doctors");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "couples");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "appointments");
        }
    }
}
