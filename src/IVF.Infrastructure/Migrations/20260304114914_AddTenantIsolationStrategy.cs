using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIsolationStrategy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRootTenant",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IsolationStrategy",
                table: "tenants",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "SharedDatabase");

            // Mark the default tenant as root
            migrationBuilder.Sql(
                "UPDATE tenants SET \"IsRootTenant\" = true WHERE \"Id\" = '00000000-0000-0000-0000-000000000001'");

            // Ensure admin user is marked as platform admin
            migrationBuilder.Sql(
                "UPDATE users SET \"IsPlatformAdmin\" = true WHERE \"Username\" = 'admin'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRootTenant",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "IsolationStrategy",
                table: "tenants");
        }
    }
}
