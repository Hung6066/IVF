using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "permission_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GroupCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    GroupDisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GroupIcon = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    GroupSortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_definitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_permission_definitions_Code",
                table: "permission_definitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_permission_definitions_GroupSortOrder_SortOrder",
                table: "permission_definitions",
                columns: new[] { "GroupSortOrder", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "permission_definitions");
        }
    }
}
