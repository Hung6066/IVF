using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddedFormFieldLinkedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "patient_concept_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConceptId = table.Column<Guid>(type: "uuid", nullable: false),
                    FormResponseId = table.Column<Guid>(type: "uuid", nullable: false),
                    FormFieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: true),
                    TextValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    NumericValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    DateValue = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BooleanValue = table.Column<bool>(type: "boolean", nullable: true),
                    JsonValue = table.Column<string>(type: "text", nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_concept_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_patient_concept_snapshots_Concepts_ConceptId",
                        column: x => x.ConceptId,
                        principalTable: "Concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_patient_concept_snapshots_FormFields_FormFieldId",
                        column: x => x.FormFieldId,
                        principalTable: "FormFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);

                    table.ForeignKey(
                        name: "FK_patient_concept_snapshots_patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_patient_concept_snapshots_treatment_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "treatment_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_patient_concept_snapshots_ConceptId_PatientId",
                table: "patient_concept_snapshots",
                columns: new[] { "ConceptId", "PatientId" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_patient_concept_snapshots_CycleId",
                table: "patient_concept_snapshots",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_patient_concept_snapshots_FormFieldId",
                table: "patient_concept_snapshots",
                column: "FormFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_patient_concept_snapshots_FormResponseId",
                table: "patient_concept_snapshots",
                column: "FormResponseId");

            migrationBuilder.CreateIndex(
                name: "IX_patient_concept_snapshots_PatientId",
                table: "patient_concept_snapshots",
                column: "PatientId",
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_patient_concept_snapshots_PatientId_ConceptId_CycleId",
                table: "patient_concept_snapshots",
                columns: new[] { "PatientId", "ConceptId", "CycleId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "patient_concept_snapshots");
        }
    }
}
