using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace IVF.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConceptLibraryWithSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FormFields_ConceptCode",
                table: "FormFields");

            migrationBuilder.DropIndex(
                name: "IX_FormFields_ConceptSystem_ConceptCode",
                table: "FormFields");

            migrationBuilder.DropIndex(
                name: "IX_FormFields_SearchVector",
                table: "FormFields");

            migrationBuilder.DropIndex(
                name: "IX_FormFieldOptions_ConceptCode",
                table: "FormFieldOptions");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "FormFields");

            migrationBuilder.DropColumn(
                name: "ConceptCode",
                table: "FormFields");

            migrationBuilder.DropColumn(
                name: "ConceptDisplay",
                table: "FormFields");

            migrationBuilder.DropColumn(
                name: "ConceptSystem",
                table: "FormFields");

            migrationBuilder.DropColumn(
                name: "ConceptCode",
                table: "FormFieldOptions");

            migrationBuilder.DropColumn(
                name: "ConceptDisplay",
                table: "FormFieldOptions");

            migrationBuilder.DropColumn(
                name: "ConceptSystem",
                table: "FormFieldOptions");

            // Manual conversion from string to uuid for FormFields.ConceptId
            // Set to NULL since existing string values won't be valid UUIDs
            migrationBuilder.Sql(@"
                ALTER TABLE ""FormFields"" 
                ALTER COLUMN ""ConceptId"" 
                TYPE uuid 
                USING NULL::uuid
            ");

            // Manual conversion from string to uuid for FormFieldOptions.ConceptId
            migrationBuilder.Sql(@"
                ALTER TABLE ""FormFieldOptions"" 
                ALTER COLUMN ""ConceptId"" 
                TYPE uuid 
                USING NULL::uuid
            ");

            migrationBuilder.CreateTable(
                name: "Concepts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Display = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    System = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "LOCAL"),
                    ConceptType = table.Column<int>(type: "integer", nullable: false),
                    SearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('english', \r\n                    coalesce(\"Code\", '') || ' ' || \r\n                    coalesce(\"Display\", '') || ' ' ||\r\n                    coalesce(\"Description\", '')\r\n                )", stored: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Concepts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConceptMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConceptId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetSystem = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetDisplay = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Relationship = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConceptMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConceptMappings_Concepts_ConceptId",
                        column: x => x.ConceptId,
                        principalTable: "Concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormFields_ConceptId",
                table: "FormFields",
                column: "ConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_FormFieldOptions_ConceptId",
                table: "FormFieldOptions",
                column: "ConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_ConceptMappings_ConceptId_TargetSystem",
                table: "ConceptMappings",
                columns: new[] { "ConceptId", "TargetSystem" });

            migrationBuilder.CreateIndex(
                name: "IX_ConceptMappings_IsActive",
                table: "ConceptMappings",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ConceptMappings_TargetSystem_TargetCode",
                table: "ConceptMappings",
                columns: new[] { "TargetSystem", "TargetCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Concepts_Code",
                table: "Concepts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Concepts_ConceptType",
                table: "Concepts",
                column: "ConceptType");

            migrationBuilder.CreateIndex(
                name: "IX_Concepts_SearchVector",
                table: "Concepts",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_Concepts_System_Code",
                table: "Concepts",
                columns: new[] { "System", "Code" });

            migrationBuilder.AddForeignKey(
                name: "FK_FormFieldOptions_Concepts_ConceptId",
                table: "FormFieldOptions",
                column: "ConceptId",
                principalTable: "Concepts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_FormFields_Concepts_ConceptId",
                table: "FormFields",
                column: "ConceptId",
                principalTable: "Concepts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FormFieldOptions_Concepts_ConceptId",
                table: "FormFieldOptions");

            migrationBuilder.DropForeignKey(
                name: "FK_FormFields_Concepts_ConceptId",
                table: "FormFields");

            migrationBuilder.DropTable(
                name: "ConceptMappings");

            migrationBuilder.DropTable(
                name: "Concepts");

            migrationBuilder.DropIndex(
                name: "IX_FormFields_ConceptId",
                table: "FormFields");

            migrationBuilder.DropIndex(
                name: "IX_FormFieldOptions_ConceptId",
                table: "FormFieldOptions");

            migrationBuilder.AlterColumn<string>(
                name: "ConceptId",
                table: "FormFields",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConceptCode",
                table: "FormFields",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConceptDisplay",
                table: "FormFields",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConceptSystem",
                table: "FormFields",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ConceptId",
                table: "FormFieldOptions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConceptCode",
                table: "FormFieldOptions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConceptDisplay",
                table: "FormFieldOptions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConceptSystem",
                table: "FormFieldOptions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "FormFields",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "to_tsvector('english', \r\n                    coalesce(\"Label\", '') || ' ' || \r\n                    coalesce(\"ConceptDisplay\", '') || ' ' || \r\n                    coalesce(\"ConceptCode\", '') || ' ' ||\r\n                    coalesce(\"HelpText\", '')\r\n                )",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormFields_ConceptCode",
                table: "FormFields",
                column: "ConceptCode");

            migrationBuilder.CreateIndex(
                name: "IX_FormFields_ConceptSystem_ConceptCode",
                table: "FormFields",
                columns: new[] { "ConceptSystem", "ConceptCode" });

            migrationBuilder.CreateIndex(
                name: "IX_FormFields_SearchVector",
                table: "FormFields",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_FormFieldOptions_ConceptCode",
                table: "FormFieldOptions",
                column: "ConceptCode");
        }
    }
}
