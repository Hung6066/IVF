using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace IVF.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConceptMappingAndFullTextSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_form_field_values_form_fields_FormFieldId",
                table: "form_field_values");

            migrationBuilder.DropForeignKey(
                name: "FK_form_fields_form_templates_FormTemplateId",
                table: "form_fields");

            migrationBuilder.DropPrimaryKey(
                name: "PK_form_fields",
                table: "form_fields");

            migrationBuilder.DropIndex(
                name: "IX_form_fields_FormTemplateId_FieldKey",
                table: "form_fields");

            migrationBuilder.RenameTable(
                name: "form_fields",
                newName: "FormFields");

            migrationBuilder.AlterColumn<string>(
                name: "ValidationRulesJson",
                table: "FormFields",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Placeholder",
                table: "FormFields",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);


            migrationBuilder.AlterColumn<string>(
                name: "OptionsJson",
                table: "FormFields",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            // Manual conversion from string to integer for FieldType with mapping
            migrationBuilder.Sql(@"
                ALTER TABLE ""FormFields"" 
                ALTER COLUMN ""FieldType"" 
                TYPE integer 
                USING (
                    CASE ""FieldType""
                        WHEN 'Text' THEN 0
                        WHEN 'Number' THEN 1
                        WHEN 'Date' THEN 2
                        WHEN 'Time' THEN 3
                        WHEN 'DateTime' THEN 4
                        WHEN 'Email' THEN 5
                        WHEN 'Phone' THEN 6
                        WHEN 'Checkbox' THEN 7
                        WHEN 'TextArea' THEN 8
                        WHEN 'Dropdown' THEN 9
                        WHEN 'Radio' THEN 10
                        WHEN 'File' THEN 11
                        WHEN 'Label' THEN 12
                        WHEN 'Section' THEN 13
                        WHEN 'Rating' THEN 14
                        ELSE 0
                    END
                )
            ");


            migrationBuilder.AlterColumn<string>(
                name: "DefaultValue",
                table: "FormFields",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ConditionalLogicJson",
                table: "FormFields",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb",
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
                name: "ConceptId",
                table: "FormFields",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConceptSystem",
                table: "FormFields",
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

            migrationBuilder.AddPrimaryKey(
                name: "PK_FormFields",
                table: "FormFields",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "FormFieldOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FormFieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Label = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    ConceptId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConceptCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConceptSystem = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConceptDisplay = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormFieldOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormFieldOptions_FormFields_FormFieldId",
                        column: x => x.FormFieldId,
                        principalTable: "FormFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormFields_ConceptCode",
                table: "FormFields",
                column: "ConceptCode");

            migrationBuilder.CreateIndex(
                name: "IX_FormFields_ConceptSystem_ConceptCode",
                table: "FormFields",
                columns: new[] { "ConceptSystem", "ConceptCode" });

            migrationBuilder.CreateIndex(
                name: "IX_FormFields_FormTemplateId",
                table: "FormFields",
                column: "FormTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_FormFields_SearchVector",
                table: "FormFields",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_FormFieldOptions_ConceptCode",
                table: "FormFieldOptions",
                column: "ConceptCode");

            migrationBuilder.CreateIndex(
                name: "IX_FormFieldOptions_FormFieldId_DisplayOrder",
                table: "FormFieldOptions",
                columns: new[] { "FormFieldId", "DisplayOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_form_field_values_FormFields_FormFieldId",
                table: "form_field_values",
                column: "FormFieldId",
                principalTable: "FormFields",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FormFields_form_templates_FormTemplateId",
                table: "FormFields",
                column: "FormTemplateId",
                principalTable: "form_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_form_field_values_FormFields_FormFieldId",
                table: "form_field_values");

            migrationBuilder.DropForeignKey(
                name: "FK_FormFields_form_templates_FormTemplateId",
                table: "FormFields");

            migrationBuilder.DropTable(
                name: "FormFieldOptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FormFields",
                table: "FormFields");

            migrationBuilder.DropIndex(
                name: "IX_FormFields_ConceptCode",
                table: "FormFields");

            migrationBuilder.DropIndex(
                name: "IX_FormFields_ConceptSystem_ConceptCode",
                table: "FormFields");

            migrationBuilder.DropIndex(
                name: "IX_FormFields_FormTemplateId",
                table: "FormFields");

            migrationBuilder.DropIndex(
                name: "IX_FormFields_SearchVector",
                table: "FormFields");

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
                name: "ConceptId",
                table: "FormFields");

            migrationBuilder.DropColumn(
                name: "ConceptSystem",
                table: "FormFields");

            migrationBuilder.RenameTable(
                name: "FormFields",
                newName: "form_fields");

            migrationBuilder.AlterColumn<string>(
                name: "ValidationRulesJson",
                table: "form_fields",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Placeholder",
                table: "form_fields",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OptionsJson",
                table: "form_fields",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FieldType",
                table: "form_fields",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "DefaultValue",
                table: "form_fields",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ConditionalLogicJson",
                table: "form_fields",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_form_fields",
                table: "form_fields",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_form_fields_FormTemplateId_FieldKey",
                table: "form_fields",
                columns: new[] { "FormTemplateId", "FieldKey" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_form_field_values_form_fields_FormFieldId",
                table: "form_field_values",
                column: "FormFieldId",
                principalTable: "form_fields",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_form_fields_form_templates_FormTemplateId",
                table: "form_fields",
                column: "FormTemplateId",
                principalTable: "form_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
