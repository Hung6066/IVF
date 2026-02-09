using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLayoutJsonToFormField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LayoutJson",
                table: "FormFields",
                type: "text",
                nullable: true);

            // Migrate existing layout data (colSpan, height) from ValidationRulesJson to LayoutJson
            migrationBuilder.Sql(@"
                UPDATE ""FormFields""
                SET ""LayoutJson"" = jsonb_build_object(
                        'colSpan', (""ValidationRulesJson""::jsonb)->>'colSpan',
                        'height', (""ValidationRulesJson""::jsonb)->>'height'
                    )::text
                WHERE ""ValidationRulesJson"" IS NOT NULL
                  AND ""ValidationRulesJson""::jsonb ? 'colSpan';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LayoutJson",
                table: "FormFields");
        }
    }
}
