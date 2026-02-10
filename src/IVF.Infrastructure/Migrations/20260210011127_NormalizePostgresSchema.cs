using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NormalizePostgresSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConceptMappings_Concepts_ConceptId",
                table: "ConceptMappings");

            migrationBuilder.DropForeignKey(
                name: "FK_form_field_values_FormFields_FormFieldId",
                table: "form_field_values");

            migrationBuilder.DropForeignKey(
                name: "FK_FormFieldOptions_Concepts_ConceptId",
                table: "FormFieldOptions");

            migrationBuilder.DropForeignKey(
                name: "FK_FormFieldOptions_FormFields_FormFieldId",
                table: "FormFieldOptions");

            migrationBuilder.DropForeignKey(
                name: "FK_FormFields_Concepts_ConceptId",
                table: "FormFields");

            migrationBuilder.DropForeignKey(
                name: "FK_FormFields_form_templates_FormTemplateId",
                table: "FormFields");

            migrationBuilder.DropForeignKey(
                name: "FK_FormFieldValueDetails_Concepts_ConceptId",
                table: "FormFieldValueDetails");

            // FK_FormFieldValueDetails_form_field_values_FormFieldValueId never existed
            // (partitioned table created outside EF). Use IF EXISTS for safety.
            migrationBuilder.Sql(@"ALTER TABLE ""FormFieldValueDetails"" DROP CONSTRAINT IF EXISTS ""FK_FormFieldValueDetails_form_field_values_FormFieldValueId"";");

            migrationBuilder.DropForeignKey(
                name: "FK_invoices_patients_PatientId",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_invoices_treatment_cycles_CycleId",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_patient_concept_snapshots_Concepts_ConceptId",
                table: "patient_concept_snapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_patient_concept_snapshots_FormFields_FormFieldId",
                table: "patient_concept_snapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientFingerprints_patients_PatientId",
                table: "PatientFingerprints");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientPhotos_patients_PatientId",
                table: "PatientPhotos");

            migrationBuilder.DropForeignKey(
                name: "FK_prescriptions_patients_PatientId",
                table: "prescriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_prescriptions_treatment_cycles_CycleId",
                table: "prescriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_prescriptions_users_DoctorId",
                table: "prescriptions");

            // Actual constraint name has capital 'P' in Patients
            migrationBuilder.DropForeignKey(
                name: "FK_semen_analyses_Patients_PatientId",
                table: "semen_analyses");

            migrationBuilder.DropForeignKey(
                name: "FK_semen_analyses_treatment_cycles_CycleId",
                table: "semen_analyses");

            migrationBuilder.DropForeignKey(
                name: "FK_sperm_donors_patients_PatientId",
                table: "sperm_donors");

            migrationBuilder.DropForeignKey(
                name: "FK_sperm_washings_patients_PatientId",
                table: "sperm_washings");

            migrationBuilder.DropForeignKey(
                name: "FK_sperm_washings_treatment_cycles_CycleId",
                table: "sperm_washings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Concepts",
                table: "Concepts");

            // ServiceCatalogs is partitioned — cannot drop/add PK. Will rename after table rename.

            migrationBuilder.DropPrimaryKey(
                name: "PK_PatientPhotos",
                table: "PatientPhotos");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PatientFingerprints",
                table: "PatientFingerprints");

            // FormFieldValueDetails is partitioned — cannot drop/add PK. Will rename after table rename.

            migrationBuilder.DropPrimaryKey(
                name: "PK_FormFields",
                table: "FormFields");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FormFieldOptions",
                table: "FormFieldOptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ConceptMappings",
                table: "ConceptMappings");

            migrationBuilder.RenameTable(
                name: "Concepts",
                newName: "concepts");

            migrationBuilder.RenameTable(
                name: "ServiceCatalogs",
                newName: "service_catalogs");

            migrationBuilder.RenameTable(
                name: "PatientPhotos",
                newName: "patient_photos");

            migrationBuilder.RenameTable(
                name: "PatientFingerprints",
                newName: "patient_fingerprints");

            migrationBuilder.RenameTable(
                name: "FormFieldValueDetails",
                newName: "form_field_value_details");

            migrationBuilder.RenameTable(
                name: "FormFields",
                newName: "form_fields");

            migrationBuilder.RenameTable(
                name: "FormFieldOptions",
                newName: "form_field_options");

            migrationBuilder.RenameTable(
                name: "ConceptMappings",
                newName: "concept_mappings");

            migrationBuilder.RenameIndex(
                name: "IX_Concepts_System_Code",
                table: "concepts",
                newName: "IX_concepts_System_Code");

            migrationBuilder.RenameIndex(
                name: "IX_Concepts_SearchVector",
                table: "concepts",
                newName: "IX_concepts_SearchVector");

            migrationBuilder.RenameIndex(
                name: "IX_Concepts_ConceptType",
                table: "concepts",
                newName: "IX_concepts_ConceptType");

            migrationBuilder.RenameIndex(
                name: "IX_Concepts_Code",
                table: "concepts",
                newName: "IX_concepts_Code");

            migrationBuilder.RenameIndex(
                name: "IX_ServiceCatalogs_IsActive",
                table: "service_catalogs",
                newName: "IX_service_catalogs_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_ServiceCatalogs_Code",
                table: "service_catalogs",
                newName: "IX_service_catalogs_Code");

            migrationBuilder.RenameIndex(
                name: "IX_ServiceCatalogs_Category",
                table: "service_catalogs",
                newName: "IX_service_catalogs_Category");

            migrationBuilder.RenameIndex(
                name: "IX_PatientPhotos_PatientId",
                table: "patient_photos",
                newName: "IX_patient_photos_PatientId");

            migrationBuilder.RenameIndex(
                name: "IX_PatientFingerprints_PatientId_FingerType",
                table: "patient_fingerprints",
                newName: "IX_patient_fingerprints_PatientId_FingerType");

            migrationBuilder.RenameIndex(
                name: "IX_FormFieldValueDetails_FormFieldValueId",
                table: "form_field_value_details",
                newName: "IX_form_field_value_details_FormFieldValueId");

            migrationBuilder.RenameIndex(
                name: "IX_FormFieldValueDetails_ConceptId",
                table: "form_field_value_details",
                newName: "IX_form_field_value_details_ConceptId");

            migrationBuilder.RenameIndex(
                name: "IX_FormFields_FormTemplateId",
                table: "form_fields",
                newName: "IX_form_fields_FormTemplateId");

            migrationBuilder.RenameIndex(
                name: "IX_FormFields_ConceptId",
                table: "form_fields",
                newName: "IX_form_fields_ConceptId");

            migrationBuilder.RenameIndex(
                name: "IX_FormFieldOptions_FormFieldId_DisplayOrder",
                table: "form_field_options",
                newName: "IX_form_field_options_FormFieldId_DisplayOrder");

            migrationBuilder.RenameIndex(
                name: "IX_FormFieldOptions_ConceptId",
                table: "form_field_options",
                newName: "IX_form_field_options_ConceptId");

            migrationBuilder.RenameIndex(
                name: "IX_ConceptMappings_TargetSystem_TargetCode",
                table: "concept_mappings",
                newName: "IX_concept_mappings_TargetSystem_TargetCode");

            migrationBuilder.RenameIndex(
                name: "IX_ConceptMappings_IsActive",
                table: "concept_mappings",
                newName: "IX_concept_mappings_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_ConceptMappings_ConceptId_TargetSystem",
                table: "concept_mappings",
                newName: "IX_concept_mappings_ConceptId_TargetSystem");

            // Convert enums from integer to string using CASE expressions
            // (PostgreSQL cannot implicitly cast integer to varchar)
            migrationBuilder.Sql(@"
                ALTER TABLE concepts
                ALTER COLUMN ""ConceptType"" TYPE character varying(30)
                USING CASE ""ConceptType""
                    WHEN 0 THEN 'Clinical'
                    WHEN 1 THEN 'Laboratory'
                    WHEN 2 THEN 'Medication'
                    WHEN 3 THEN 'Diagnosis'
                    WHEN 4 THEN 'Procedure'
                    WHEN 5 THEN 'Anatomical'
                    WHEN 6 THEN 'Administrative'
                    ELSE 'Clinical'
                END;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE patient_fingerprints
                ALTER COLUMN ""SdkType"" TYPE character varying(30)
                USING CASE ""SdkType""
                    WHEN 1 THEN 'DigitalPersona'
                    WHEN 2 THEN 'SecuGen'
                    ELSE 'DigitalPersona'
                END;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE patient_fingerprints
                ALTER COLUMN ""FingerType"" TYPE character varying(30)
                USING CASE ""FingerType""
                    WHEN 1 THEN 'LeftThumb'
                    WHEN 2 THEN 'LeftIndex'
                    WHEN 3 THEN 'LeftMiddle'
                    WHEN 4 THEN 'LeftRing'
                    WHEN 5 THEN 'LeftPinky'
                    WHEN 6 THEN 'RightThumb'
                    WHEN 7 THEN 'RightIndex'
                    WHEN 8 THEN 'RightMiddle'
                    WHEN 9 THEN 'RightRing'
                    WHEN 10 THEN 'RightPinky'
                    ELSE 'RightIndex'
                END;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE form_fields
                ALTER COLUMN ""FieldType"" TYPE character varying(30)
                USING CASE ""FieldType""
                    WHEN 1 THEN 'Text'
                    WHEN 2 THEN 'TextArea'
                    WHEN 3 THEN 'Number'
                    WHEN 4 THEN 'Decimal'
                    WHEN 5 THEN 'Date'
                    WHEN 6 THEN 'DateTime'
                    WHEN 7 THEN 'Time'
                    WHEN 8 THEN 'Dropdown'
                    WHEN 9 THEN 'MultiSelect'
                    WHEN 10 THEN 'Radio'
                    WHEN 11 THEN 'Checkbox'
                    WHEN 12 THEN 'FileUpload'
                    WHEN 13 THEN 'Rating'
                    WHEN 14 THEN 'Section'
                    WHEN 15 THEN 'Label'
                    WHEN 16 THEN 'Tags'
                    WHEN 17 THEN 'PageBreak'
                    WHEN 18 THEN 'Address'
                    WHEN 19 THEN 'Hidden'
                    WHEN 20 THEN 'Slider'
                    WHEN 21 THEN 'Calculated'
                    WHEN 22 THEN 'RichText'
                    WHEN 23 THEN 'Signature'
                    WHEN 24 THEN 'Lookup'
                    WHEN 25 THEN 'Repeater'
                    ELSE 'Text'
                END;
            ");

            migrationBuilder.AddPrimaryKey(
                name: "PK_concepts",
                table: "concepts",
                column: "Id");

            // Rename PK on partitioned table (cannot drop/recreate)
            migrationBuilder.Sql(@"ALTER TABLE service_catalogs RENAME CONSTRAINT ""PK_ServiceCatalogs"" TO ""PK_service_catalogs"";");

            migrationBuilder.AddPrimaryKey(
                name: "PK_patient_photos",
                table: "patient_photos",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_patient_fingerprints",
                table: "patient_fingerprints",
                column: "Id");

            // Rename PK on partitioned table (cannot drop/recreate)
            migrationBuilder.Sql(@"ALTER TABLE form_field_value_details RENAME CONSTRAINT ""PK_FormFieldValueDetails"" TO ""PK_form_field_value_details"";");

            migrationBuilder.AddPrimaryKey(
                name: "PK_form_fields",
                table: "form_fields",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_form_field_options",
                table: "form_field_options",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_concept_mappings",
                table: "concept_mappings",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "linked_field_sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetFieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceFieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    FlowType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_linked_field_sources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_linked_field_sources_form_fields_SourceFieldId",
                        column: x => x.SourceFieldId,
                        principalTable: "form_fields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_linked_field_sources_form_fields_TargetFieldId",
                        column: x => x.TargetFieldId,
                        principalTable: "form_fields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_linked_field_sources_form_templates_SourceTemplateId",
                        column: x => x.SourceTemplateId,
                        principalTable: "form_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_EntityType_EntityId",
                table: "notifications",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_form_fields_FormTemplateId_DisplayOrder",
                table: "form_fields",
                columns: new[] { "FormTemplateId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_linked_field_sources_SourceFieldId",
                table: "linked_field_sources",
                column: "SourceFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_linked_field_sources_SourceTemplateId",
                table: "linked_field_sources",
                column: "SourceTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_linked_field_sources_TargetFieldId",
                table: "linked_field_sources",
                column: "TargetFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_linked_field_sources_TargetFieldId_SourceFieldId",
                table: "linked_field_sources",
                columns: new[] { "TargetFieldId", "SourceFieldId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_concept_mappings_concepts_ConceptId",
                table: "concept_mappings",
                column: "ConceptId",
                principalTable: "concepts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_form_field_options_concepts_ConceptId",
                table: "form_field_options",
                column: "ConceptId",
                principalTable: "concepts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_form_field_options_form_fields_FormFieldId",
                table: "form_field_options",
                column: "FormFieldId",
                principalTable: "form_fields",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_form_field_value_details_concepts_ConceptId",
                table: "form_field_value_details",
                column: "ConceptId",
                principalTable: "concepts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Cannot create FK from form_field_value_details → form_field_values
            // because form_field_values is partitioned with composite PK (Id, CreatedAt).
            // PostgreSQL requires FK to reference the complete PK of a partitioned table.
            // The logical relationship is maintained via EF navigation properties.

            migrationBuilder.AddForeignKey(
                name: "FK_form_field_values_form_fields_FormFieldId",
                table: "form_field_values",
                column: "FormFieldId",
                principalTable: "form_fields",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_form_fields_concepts_ConceptId",
                table: "form_fields",
                column: "ConceptId",
                principalTable: "concepts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_form_fields_form_templates_FormTemplateId",
                table: "form_fields",
                column: "FormTemplateId",
                principalTable: "form_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_patients_PatientId",
                table: "invoices",
                column: "PatientId",
                principalTable: "patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_treatment_cycles_CycleId",
                table: "invoices",
                column: "CycleId",
                principalTable: "treatment_cycles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_patient_concept_snapshots_concepts_ConceptId",
                table: "patient_concept_snapshots",
                column: "ConceptId",
                principalTable: "concepts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_patient_concept_snapshots_form_fields_FormFieldId",
                table: "patient_concept_snapshots",
                column: "FormFieldId",
                principalTable: "form_fields",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_patient_fingerprints_patients_PatientId",
                table: "patient_fingerprints",
                column: "PatientId",
                principalTable: "patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_patient_photos_patients_PatientId",
                table: "patient_photos",
                column: "PatientId",
                principalTable: "patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_prescriptions_patients_PatientId",
                table: "prescriptions",
                column: "PatientId",
                principalTable: "patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_prescriptions_treatment_cycles_CycleId",
                table: "prescriptions",
                column: "CycleId",
                principalTable: "treatment_cycles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_prescriptions_users_DoctorId",
                table: "prescriptions",
                column: "DoctorId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_semen_analyses_patients_PatientId",
                table: "semen_analyses",
                column: "PatientId",
                principalTable: "patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_semen_analyses_treatment_cycles_CycleId",
                table: "semen_analyses",
                column: "CycleId",
                principalTable: "treatment_cycles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sperm_donors_patients_PatientId",
                table: "sperm_donors",
                column: "PatientId",
                principalTable: "patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sperm_washings_patients_PatientId",
                table: "sperm_washings",
                column: "PatientId",
                principalTable: "patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sperm_washings_treatment_cycles_CycleId",
                table: "sperm_washings",
                column: "CycleId",
                principalTable: "treatment_cycles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_concept_mappings_concepts_ConceptId",
                table: "concept_mappings");

            migrationBuilder.DropForeignKey(
                name: "FK_form_field_options_concepts_ConceptId",
                table: "form_field_options");

            migrationBuilder.DropForeignKey(
                name: "FK_form_field_options_form_fields_FormFieldId",
                table: "form_field_options");

            migrationBuilder.DropForeignKey(
                name: "FK_form_field_value_details_concepts_ConceptId",
                table: "form_field_value_details");

            // Use IF EXISTS since this FK was added by the Up() migration and may not exist on rollback failure
            migrationBuilder.Sql(@"ALTER TABLE form_field_value_details DROP CONSTRAINT IF EXISTS ""FK_form_field_value_details_form_field_values_FormFieldValueId"";");

            migrationBuilder.DropForeignKey(
                name: "FK_form_field_values_form_fields_FormFieldId",
                table: "form_field_values");

            migrationBuilder.DropForeignKey(
                name: "FK_form_fields_concepts_ConceptId",
                table: "form_fields");

            migrationBuilder.DropForeignKey(
                name: "FK_form_fields_form_templates_FormTemplateId",
                table: "form_fields");

            migrationBuilder.DropForeignKey(
                name: "FK_invoices_patients_PatientId",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_invoices_treatment_cycles_CycleId",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_patient_concept_snapshots_concepts_ConceptId",
                table: "patient_concept_snapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_patient_concept_snapshots_form_fields_FormFieldId",
                table: "patient_concept_snapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_patient_fingerprints_patients_PatientId",
                table: "patient_fingerprints");

            migrationBuilder.DropForeignKey(
                name: "FK_patient_photos_patients_PatientId",
                table: "patient_photos");

            migrationBuilder.DropForeignKey(
                name: "FK_prescriptions_patients_PatientId",
                table: "prescriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_prescriptions_treatment_cycles_CycleId",
                table: "prescriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_prescriptions_users_DoctorId",
                table: "prescriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_semen_analyses_patients_PatientId",
                table: "semen_analyses");

            migrationBuilder.DropForeignKey(
                name: "FK_semen_analyses_treatment_cycles_CycleId",
                table: "semen_analyses");

            migrationBuilder.DropForeignKey(
                name: "FK_sperm_donors_patients_PatientId",
                table: "sperm_donors");

            migrationBuilder.DropForeignKey(
                name: "FK_sperm_washings_patients_PatientId",
                table: "sperm_washings");

            migrationBuilder.DropForeignKey(
                name: "FK_sperm_washings_treatment_cycles_CycleId",
                table: "sperm_washings");

            migrationBuilder.DropTable(
                name: "linked_field_sources");

            migrationBuilder.DropIndex(
                name: "IX_notifications_EntityType_EntityId",
                table: "notifications");

            migrationBuilder.DropPrimaryKey(
                name: "PK_concepts",
                table: "concepts");

            // ServiceCatalogs is partitioned — will rename PK after table rename

            migrationBuilder.DropPrimaryKey(
                name: "PK_patient_photos",
                table: "patient_photos");

            migrationBuilder.DropPrimaryKey(
                name: "PK_patient_fingerprints",
                table: "patient_fingerprints");

            migrationBuilder.DropPrimaryKey(
                name: "PK_form_fields",
                table: "form_fields");

            migrationBuilder.DropIndex(
                name: "IX_form_fields_FormTemplateId_DisplayOrder",
                table: "form_fields");

            // FormFieldValueDetails is partitioned — will rename PK after table rename

            migrationBuilder.DropPrimaryKey(
                name: "PK_form_field_options",
                table: "form_field_options");

            migrationBuilder.DropPrimaryKey(
                name: "PK_concept_mappings",
                table: "concept_mappings");

            migrationBuilder.RenameTable(
                name: "concepts",
                newName: "Concepts");

            migrationBuilder.RenameTable(
                name: "service_catalogs",
                newName: "ServiceCatalogs");

            migrationBuilder.RenameTable(
                name: "patient_photos",
                newName: "PatientPhotos");

            migrationBuilder.RenameTable(
                name: "patient_fingerprints",
                newName: "PatientFingerprints");

            migrationBuilder.RenameTable(
                name: "form_fields",
                newName: "FormFields");

            migrationBuilder.RenameTable(
                name: "form_field_value_details",
                newName: "FormFieldValueDetails");

            migrationBuilder.RenameTable(
                name: "form_field_options",
                newName: "FormFieldOptions");

            migrationBuilder.RenameTable(
                name: "concept_mappings",
                newName: "ConceptMappings");

            migrationBuilder.RenameIndex(
                name: "IX_concepts_System_Code",
                table: "Concepts",
                newName: "IX_Concepts_System_Code");

            migrationBuilder.RenameIndex(
                name: "IX_concepts_SearchVector",
                table: "Concepts",
                newName: "IX_Concepts_SearchVector");

            migrationBuilder.RenameIndex(
                name: "IX_concepts_ConceptType",
                table: "Concepts",
                newName: "IX_Concepts_ConceptType");

            migrationBuilder.RenameIndex(
                name: "IX_concepts_Code",
                table: "Concepts",
                newName: "IX_Concepts_Code");

            migrationBuilder.RenameIndex(
                name: "IX_service_catalogs_IsActive",
                table: "ServiceCatalogs",
                newName: "IX_ServiceCatalogs_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_service_catalogs_Code",
                table: "ServiceCatalogs",
                newName: "IX_ServiceCatalogs_Code");

            migrationBuilder.RenameIndex(
                name: "IX_service_catalogs_Category",
                table: "ServiceCatalogs",
                newName: "IX_ServiceCatalogs_Category");

            migrationBuilder.RenameIndex(
                name: "IX_patient_photos_PatientId",
                table: "PatientPhotos",
                newName: "IX_PatientPhotos_PatientId");

            migrationBuilder.RenameIndex(
                name: "IX_patient_fingerprints_PatientId_FingerType",
                table: "PatientFingerprints",
                newName: "IX_PatientFingerprints_PatientId_FingerType");

            migrationBuilder.RenameIndex(
                name: "IX_form_fields_FormTemplateId",
                table: "FormFields",
                newName: "IX_FormFields_FormTemplateId");

            migrationBuilder.RenameIndex(
                name: "IX_form_fields_ConceptId",
                table: "FormFields",
                newName: "IX_FormFields_ConceptId");

            migrationBuilder.RenameIndex(
                name: "IX_form_field_value_details_FormFieldValueId",
                table: "FormFieldValueDetails",
                newName: "IX_FormFieldValueDetails_FormFieldValueId");

            migrationBuilder.RenameIndex(
                name: "IX_form_field_value_details_ConceptId",
                table: "FormFieldValueDetails",
                newName: "IX_FormFieldValueDetails_ConceptId");

            migrationBuilder.RenameIndex(
                name: "IX_form_field_options_FormFieldId_DisplayOrder",
                table: "FormFieldOptions",
                newName: "IX_FormFieldOptions_FormFieldId_DisplayOrder");

            migrationBuilder.RenameIndex(
                name: "IX_form_field_options_ConceptId",
                table: "FormFieldOptions",
                newName: "IX_FormFieldOptions_ConceptId");

            migrationBuilder.RenameIndex(
                name: "IX_concept_mappings_TargetSystem_TargetCode",
                table: "ConceptMappings",
                newName: "IX_ConceptMappings_TargetSystem_TargetCode");

            migrationBuilder.RenameIndex(
                name: "IX_concept_mappings_IsActive",
                table: "ConceptMappings",
                newName: "IX_ConceptMappings_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_concept_mappings_ConceptId_TargetSystem",
                table: "ConceptMappings",
                newName: "IX_ConceptMappings_ConceptId_TargetSystem");

            // Revert enums from string back to integer using CASE expressions
            migrationBuilder.Sql(@"
                ALTER TABLE ""Concepts""
                ALTER COLUMN ""ConceptType"" TYPE integer
                USING CASE ""ConceptType""
                    WHEN 'Clinical' THEN 0
                    WHEN 'Laboratory' THEN 1
                    WHEN 'Medication' THEN 2
                    WHEN 'Diagnosis' THEN 3
                    WHEN 'Procedure' THEN 4
                    WHEN 'Anatomical' THEN 5
                    WHEN 'Administrative' THEN 6
                    ELSE 0
                END;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""PatientFingerprints""
                ALTER COLUMN ""SdkType"" TYPE integer
                USING CASE ""SdkType""
                    WHEN 'DigitalPersona' THEN 1
                    WHEN 'SecuGen' THEN 2
                    ELSE 1
                END;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""PatientFingerprints""
                ALTER COLUMN ""FingerType"" TYPE integer
                USING CASE ""FingerType""
                    WHEN 'LeftThumb' THEN 1
                    WHEN 'LeftIndex' THEN 2
                    WHEN 'LeftMiddle' THEN 3
                    WHEN 'LeftRing' THEN 4
                    WHEN 'LeftPinky' THEN 5
                    WHEN 'RightThumb' THEN 6
                    WHEN 'RightIndex' THEN 7
                    WHEN 'RightMiddle' THEN 8
                    WHEN 'RightRing' THEN 9
                    WHEN 'RightPinky' THEN 10
                    ELSE 7
                END;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""FormFields""
                ALTER COLUMN ""FieldType"" TYPE integer
                USING CASE ""FieldType""
                    WHEN 'Text' THEN 1
                    WHEN 'TextArea' THEN 2
                    WHEN 'Number' THEN 3
                    WHEN 'Decimal' THEN 4
                    WHEN 'Date' THEN 5
                    WHEN 'DateTime' THEN 6
                    WHEN 'Time' THEN 7
                    WHEN 'Dropdown' THEN 8
                    WHEN 'MultiSelect' THEN 9
                    WHEN 'Radio' THEN 10
                    WHEN 'Checkbox' THEN 11
                    WHEN 'FileUpload' THEN 12
                    WHEN 'Rating' THEN 13
                    WHEN 'Section' THEN 14
                    WHEN 'Label' THEN 15
                    WHEN 'Tags' THEN 16
                    WHEN 'PageBreak' THEN 17
                    WHEN 'Address' THEN 18
                    WHEN 'Hidden' THEN 19
                    WHEN 'Slider' THEN 20
                    WHEN 'Calculated' THEN 21
                    WHEN 'RichText' THEN 22
                    WHEN 'Signature' THEN 23
                    WHEN 'Lookup' THEN 24
                    WHEN 'Repeater' THEN 25
                    ELSE 1
                END;
            ");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Concepts",
                table: "Concepts",
                column: "Id");

            // Rename PK on partitioned table (cannot drop/recreate)
            migrationBuilder.Sql(@"ALTER TABLE ""ServiceCatalogs"" RENAME CONSTRAINT ""PK_service_catalogs"" TO ""PK_ServiceCatalogs"";");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PatientPhotos",
                table: "PatientPhotos",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PatientFingerprints",
                table: "PatientFingerprints",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FormFields",
                table: "FormFields",
                column: "Id");

            // Rename PK on partitioned table (cannot drop/recreate)
            migrationBuilder.Sql(@"ALTER TABLE ""FormFieldValueDetails"" RENAME CONSTRAINT ""PK_form_field_value_details"" TO ""PK_FormFieldValueDetails"";");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FormFieldOptions",
                table: "FormFieldOptions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ConceptMappings",
                table: "ConceptMappings",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConceptMappings_Concepts_ConceptId",
                table: "ConceptMappings",
                column: "ConceptId",
                principalTable: "Concepts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_form_field_values_FormFields_FormFieldId",
                table: "form_field_values",
                column: "FormFieldId",
                principalTable: "FormFields",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FormFieldOptions_Concepts_ConceptId",
                table: "FormFieldOptions",
                column: "ConceptId",
                principalTable: "Concepts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_FormFieldOptions_FormFields_FormFieldId",
                table: "FormFieldOptions",
                column: "FormFieldId",
                principalTable: "FormFields",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FormFields_Concepts_ConceptId",
                table: "FormFields",
                column: "ConceptId",
                principalTable: "Concepts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_FormFields_form_templates_FormTemplateId",
                table: "FormFields",
                column: "FormTemplateId",
                principalTable: "form_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FormFieldValueDetails_Concepts_ConceptId",
                table: "FormFieldValueDetails",
                column: "ConceptId",
                principalTable: "Concepts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // FK_FormFieldValueDetails_form_field_values_FormFieldValueId was never in the original DB
            // (partitioned table created outside EF), so we don't recreate it on rollback.

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_patients_PatientId",
                table: "invoices",
                column: "PatientId",
                principalTable: "patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_treatment_cycles_CycleId",
                table: "invoices",
                column: "CycleId",
                principalTable: "treatment_cycles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_patient_concept_snapshots_Concepts_ConceptId",
                table: "patient_concept_snapshots",
                column: "ConceptId",
                principalTable: "Concepts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_patient_concept_snapshots_FormFields_FormFieldId",
                table: "patient_concept_snapshots",
                column: "FormFieldId",
                principalTable: "FormFields",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientFingerprints_patients_PatientId",
                table: "PatientFingerprints",
                column: "PatientId",
                principalTable: "patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientPhotos_patients_PatientId",
                table: "PatientPhotos",
                column: "PatientId",
                principalTable: "patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_prescriptions_patients_PatientId",
                table: "prescriptions",
                column: "PatientId",
                principalTable: "patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_prescriptions_treatment_cycles_CycleId",
                table: "prescriptions",
                column: "CycleId",
                principalTable: "treatment_cycles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_prescriptions_users_DoctorId",
                table: "prescriptions",
                column: "DoctorId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_semen_analyses_patients_PatientId",
                table: "semen_analyses",
                column: "PatientId",
                principalTable: "patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_semen_analyses_treatment_cycles_CycleId",
                table: "semen_analyses",
                column: "CycleId",
                principalTable: "treatment_cycles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_sperm_donors_patients_PatientId",
                table: "sperm_donors",
                column: "PatientId",
                principalTable: "patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_sperm_washings_patients_PatientId",
                table: "sperm_washings",
                column: "PatientId",
                principalTable: "patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_sperm_washings_treatment_cycles_CycleId",
                table: "sperm_washings",
                column: "CycleId",
                principalTable: "treatment_cycles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
