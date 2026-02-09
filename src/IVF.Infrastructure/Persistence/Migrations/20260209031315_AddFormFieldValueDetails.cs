using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFormFieldValueDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FormFieldValueDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FormFieldValueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Label = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ConceptId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormFieldValueDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormFieldValueDetails_Concepts_ConceptId",
                        column: x => x.ConceptId,
                        principalTable: "Concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FormFieldValueDetails_form_field_values_FormFieldValueId",
                        column: x => x.FormFieldValueId,
                        principalTable: "form_field_values",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormFieldValueDetails_ConceptId",
                table: "FormFieldValueDetails",
                column: "ConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_FormFieldValueDetails_FormFieldValueId",
                table: "FormFieldValueDetails",
                column: "FormFieldValueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FormFieldValueDetails");
        }
    }
}
