using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicFeaturePlanMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequiredFeatureCode",
                table: "menu_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "feature_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Icon = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "core"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "plan_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Plan = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MonthlyPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "VND"),
                    Duration = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MaxUsers = table.Column<int>(type: "integer", nullable: false),
                    MaxPatientsPerMonth = table.Column<int>(type: "integer", nullable: false),
                    StorageLimitMb = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_features",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_features", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_features_feature_definitions_FeatureDefinitionId",
                        column: x => x.FeatureDefinitionId,
                        principalTable: "feature_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tenant_features_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plan_features",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_features", x => x.Id);
                    table.ForeignKey(
                        name: "FK_plan_features_feature_definitions_FeatureDefinitionId",
                        column: x => x.FeatureDefinitionId,
                        principalTable: "feature_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_plan_features_plan_definitions_PlanDefinitionId",
                        column: x => x.PlanDefinitionId,
                        principalTable: "plan_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_feature_definitions_Code",
                table: "feature_definitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plan_definitions_Plan",
                table: "plan_definitions",
                column: "Plan",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plan_features_FeatureDefinitionId",
                table: "plan_features",
                column: "FeatureDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_plan_features_PlanDefinitionId_FeatureDefinitionId",
                table: "plan_features",
                columns: new[] { "PlanDefinitionId", "FeatureDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_features_FeatureDefinitionId",
                table: "tenant_features",
                column: "FeatureDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_features_TenantId_FeatureDefinitionId",
                table: "tenant_features",
                columns: new[] { "TenantId", "FeatureDefinitionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plan_features");

            migrationBuilder.DropTable(
                name: "tenant_features");

            migrationBuilder.DropTable(
                name: "plan_definitions");

            migrationBuilder.DropTable(
                name: "feature_definitions");

            migrationBuilder.DropColumn(
                name: "RequiredFeatureCode",
                table: "menu_items");
        }
    }
}
