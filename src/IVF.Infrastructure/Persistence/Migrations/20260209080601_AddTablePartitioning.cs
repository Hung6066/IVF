using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds PostgreSQL native table partitioning for high-volume tables:
    /// - form_responses: RANGE by CreatedAt (monthly)
    /// - form_field_values: RANGE by CreatedAt (monthly)
    /// - FormFieldValueDetails: RANGE by CreatedAt (monthly)
    /// - semen_analyses: RANGE by AnalysisDate (monthly)
    /// - ServiceCatalogs: LIST by Category
    /// Also fixes FK on FormFieldValueDetails from Cascade to Restrict.
    /// </summary>
    public partial class AddTablePartitioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix FK behavior first (EF Core detected change)
            migrationBuilder.DropForeignKey(
                name: "FK_FormFieldValueDetails_form_field_values_FormFieldValueId",
                table: "FormFieldValueDetails");

            migrationBuilder.AddForeignKey(
                name: "FK_FormFieldValueDetails_form_field_values_FormFieldValueId",
                table: "FormFieldValueDetails",
                column: "FormFieldValueId",
                principalTable: "form_field_values",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // =================================================================
            // 1. form_responses — RANGE partition by CreatedAt
            // =================================================================
            migrationBuilder.Sql(@"
                -- Drop dependent FK from form_field_values
                ALTER TABLE form_field_values DROP CONSTRAINT IF EXISTS ""FK_form_field_values_form_responses_FormResponseId"";

                -- Rename old table and its PK constraint
                ALTER TABLE form_responses RENAME TO form_responses_old;
                ALTER TABLE form_responses_old RENAME CONSTRAINT ""PK_form_responses"" TO ""PK_form_responses_old"";

                -- Drop indexes on old table
                DROP INDEX IF EXISTS ""IX_form_responses_FormTemplateId"";
                DROP INDEX IF EXISTS ""IX_form_responses_PatientId"";
                DROP INDEX IF EXISTS ""IX_form_responses_SubmittedAt"";

                -- Create partitioned table
                CREATE TABLE form_responses (
                    ""Id"" uuid NOT NULL,
                    ""FormTemplateId"" uuid NOT NULL,
                    ""PatientId"" uuid,
                    ""CycleId"" uuid,
                    ""SubmittedByUserId"" uuid,
                    ""SubmittedAt"" timestamp with time zone,
                    ""Status"" character varying(20) NOT NULL,
                    ""Notes"" character varying(2000),
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""UpdatedAt"" timestamp with time zone,
                    ""IsDeleted"" boolean NOT NULL DEFAULT false,
                    CONSTRAINT ""PK_form_responses"" PRIMARY KEY (""Id"", ""CreatedAt"")
                ) PARTITION BY RANGE (""CreatedAt"");

                -- Create monthly partitions (2025-2027) BEFORE copying data
                DO $$
                DECLARE
                    start_date DATE := '2025-01-01';
                    end_date DATE;
                    partition_name TEXT;
                BEGIN
                    FOR i IN 0..35 LOOP
                        end_date := start_date + INTERVAL '1 month';
                        partition_name := 'form_responses_' || TO_CHAR(start_date, 'YYYY_MM');
                        EXECUTE format(
                            'CREATE TABLE IF NOT EXISTS %I PARTITION OF form_responses FOR VALUES FROM (%L) TO (%L)',
                            partition_name, start_date, end_date
                        );
                        start_date := end_date;
                    END LOOP;
                END $$;

                CREATE TABLE IF NOT EXISTS form_responses_default PARTITION OF form_responses DEFAULT;

                -- Copy data
                INSERT INTO form_responses SELECT * FROM form_responses_old;

                -- Drop old table
                DROP TABLE form_responses_old;

                -- Recreate indexes
                CREATE INDEX ""IX_form_responses_FormTemplateId"" ON form_responses (""FormTemplateId"");
                CREATE INDEX ""IX_form_responses_PatientId"" ON form_responses (""PatientId"");
                CREATE INDEX ""IX_form_responses_SubmittedAt"" ON form_responses (""SubmittedAt"");
                CREATE INDEX ""IX_form_responses_CreatedAt"" ON form_responses (""CreatedAt"");
                CREATE INDEX ""IX_form_responses_IsDeleted"" ON form_responses (""IsDeleted"") WHERE ""IsDeleted"" = false;

                -- Recreate FKs
                -- NOTE: FK from form_field_values → form_responses is intentionally NOT recreated
                -- because partitioned table PK is (Id, CreatedAt) and FK can only reference simple PK.
                -- Referential integrity is enforced at the application level.
                ALTER TABLE form_responses ADD CONSTRAINT ""FK_form_responses_form_templates_FormTemplateId""
                    FOREIGN KEY (""FormTemplateId"") REFERENCES form_templates (""Id"") ON DELETE RESTRICT;
                ALTER TABLE form_responses ADD CONSTRAINT ""FK_form_responses_patients_PatientId""
                    FOREIGN KEY (""PatientId"") REFERENCES patients (""Id"") ON DELETE SET NULL;
                ALTER TABLE form_responses ADD CONSTRAINT ""FK_form_responses_treatment_cycles_CycleId""
                    FOREIGN KEY (""CycleId"") REFERENCES treatment_cycles (""Id"") ON DELETE SET NULL;
                ALTER TABLE form_responses ADD CONSTRAINT ""FK_form_responses_users_SubmittedByUserId""
                    FOREIGN KEY (""SubmittedByUserId"") REFERENCES users (""Id"") ON DELETE SET NULL;
            ");

            // =================================================================
            // 2. form_field_values — RANGE partition by CreatedAt
            // =================================================================
            migrationBuilder.Sql(@"
                -- Drop dependent FK from FormFieldValueDetails
                ALTER TABLE ""FormFieldValueDetails"" DROP CONSTRAINT IF EXISTS ""FK_FormFieldValueDetails_form_field_values_FormFieldValueId"";

                -- Rename old table and its PK constraint
                ALTER TABLE form_field_values RENAME TO form_field_values_old;
                ALTER TABLE form_field_values_old RENAME CONSTRAINT ""PK_form_field_values"" TO ""PK_form_field_values_old"";

                -- Drop indexes
                DROP INDEX IF EXISTS ""IX_form_field_values_FormResponseId_FormFieldId"";
                DROP INDEX IF EXISTS ""IX_form_field_values_FormFieldId"";

                -- Create partitioned table
                CREATE TABLE form_field_values (
                    ""Id"" uuid NOT NULL,
                    ""FormResponseId"" uuid NOT NULL,
                    ""FormFieldId"" uuid NOT NULL,
                    ""TextValue"" character varying(4000),
                    ""NumericValue"" numeric(18,6),
                    ""DateValue"" timestamp with time zone,
                    ""BooleanValue"" boolean,
                    ""JsonValue"" text,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""UpdatedAt"" timestamp with time zone,
                    ""IsDeleted"" boolean NOT NULL DEFAULT false,
                    CONSTRAINT ""PK_form_field_values"" PRIMARY KEY (""Id"", ""CreatedAt"")
                ) PARTITION BY RANGE (""CreatedAt"");

                -- Create monthly partitions (2025-2027) BEFORE copying data
                DO $$
                DECLARE
                    start_date DATE := '2025-01-01';
                    end_date DATE;
                    partition_name TEXT;
                BEGIN
                    FOR i IN 0..35 LOOP
                        end_date := start_date + INTERVAL '1 month';
                        partition_name := 'form_field_values_' || TO_CHAR(start_date, 'YYYY_MM');
                        EXECUTE format(
                            'CREATE TABLE IF NOT EXISTS %I PARTITION OF form_field_values FOR VALUES FROM (%L) TO (%L)',
                            partition_name, start_date, end_date
                        );
                        start_date := end_date;
                    END LOOP;
                END $$;

                CREATE TABLE IF NOT EXISTS form_field_values_default PARTITION OF form_field_values DEFAULT;

                -- Copy data
                INSERT INTO form_field_values SELECT * FROM form_field_values_old;

                -- Drop old table
                DROP TABLE form_field_values_old;

                -- Recreate indexes
                CREATE UNIQUE INDEX ""IX_form_field_values_FormResponseId_FormFieldId""
                    ON form_field_values (""FormResponseId"", ""FormFieldId"", ""CreatedAt"");
                CREATE INDEX ""IX_form_field_values_FormFieldId"" ON form_field_values (""FormFieldId"");
                CREATE INDEX ""IX_form_field_values_IsDeleted"" ON form_field_values (""IsDeleted"") WHERE ""IsDeleted"" = false;

                -- FK to FormFields (non-partitioned table, safe to reference)
                ALTER TABLE form_field_values ADD CONSTRAINT ""FK_form_field_values_FormFields_FormFieldId""
                    FOREIGN KEY (""FormFieldId"") REFERENCES ""FormFields"" (""Id"") ON DELETE CASCADE;

                -- NOTE: FK from FormFieldValueDetails → form_field_values is intentionally NOT recreated
                -- because partitioned table PK is (Id, CreatedAt) and FK can only reference simple PK.
            ");

            // =================================================================
            // 3. FormFieldValueDetails — RANGE partition by CreatedAt
            // =================================================================
            migrationBuilder.Sql(@"
                -- Drop FK (recreated above, drop it again for partitioning)
                ALTER TABLE ""FormFieldValueDetails"" DROP CONSTRAINT IF EXISTS ""FK_FormFieldValueDetails_form_field_values_FormFieldValueId"";
                ALTER TABLE ""FormFieldValueDetails"" DROP CONSTRAINT IF EXISTS ""FK_FormFieldValueDetails_Concepts_ConceptId"";

                -- Rename old table and its PK constraint
                ALTER TABLE ""FormFieldValueDetails"" RENAME TO ""FormFieldValueDetails_old"";
                ALTER TABLE ""FormFieldValueDetails_old"" RENAME CONSTRAINT ""PK_FormFieldValueDetails"" TO ""PK_FormFieldValueDetails_old"";

                -- Create partitioned table
                CREATE TABLE ""FormFieldValueDetails"" (
                    ""Id"" uuid NOT NULL,
                    ""FormFieldValueId"" uuid NOT NULL,
                    ""Value"" character varying(1000) NOT NULL,
                    ""Label"" character varying(1000),
                    ""ConceptId"" uuid,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""UpdatedAt"" timestamp with time zone,
                    ""IsDeleted"" boolean NOT NULL DEFAULT false,
                    CONSTRAINT ""PK_FormFieldValueDetails"" PRIMARY KEY (""Id"", ""CreatedAt"")
                ) PARTITION BY RANGE (""CreatedAt"");

                -- Create monthly partitions (2025-2027) BEFORE copying data
                DO $$
                DECLARE
                    start_date DATE := '2025-01-01';
                    end_date DATE;
                    partition_name TEXT;
                BEGIN
                    FOR i IN 0..35 LOOP
                        end_date := start_date + INTERVAL '1 month';
                        partition_name := 'FormFieldValueDetails_' || TO_CHAR(start_date, 'YYYY_MM');
                        EXECUTE format(
                            'CREATE TABLE IF NOT EXISTS %I PARTITION OF ""FormFieldValueDetails"" FOR VALUES FROM (%L) TO (%L)',
                            partition_name, start_date, end_date
                        );
                        start_date := end_date;
                    END LOOP;
                END $$;

                CREATE TABLE IF NOT EXISTS ""FormFieldValueDetails_default"" PARTITION OF ""FormFieldValueDetails"" DEFAULT;

                -- Copy data
                INSERT INTO ""FormFieldValueDetails"" SELECT * FROM ""FormFieldValueDetails_old"";

                -- Drop old table
                DROP TABLE ""FormFieldValueDetails_old"";

                -- Recreate indexes
                CREATE INDEX ""IX_FormFieldValueDetails_FormFieldValueId"" ON ""FormFieldValueDetails"" (""FormFieldValueId"");
                CREATE INDEX ""IX_FormFieldValueDetails_ConceptId"" ON ""FormFieldValueDetails"" (""ConceptId"");
                CREATE INDEX ""IX_FormFieldValueDetails_IsDeleted"" ON ""FormFieldValueDetails"" (""IsDeleted"") WHERE ""IsDeleted"" = false;

                -- Recreate FKs
                ALTER TABLE ""FormFieldValueDetails"" ADD CONSTRAINT ""FK_FormFieldValueDetails_Concepts_ConceptId""
                    FOREIGN KEY (""ConceptId"") REFERENCES ""Concepts"" (""Id"") ON DELETE SET NULL;
            ");

            // =================================================================
            // 4. semen_analyses — RANGE partition by AnalysisDate
            // =================================================================
            migrationBuilder.Sql(@"
                -- Rename old table and its PK constraint
                ALTER TABLE semen_analyses RENAME TO semen_analyses_old;
                ALTER TABLE semen_analyses_old RENAME CONSTRAINT ""PK_semen_analyses"" TO ""PK_semen_analyses_old"";

                -- Drop indexes
                DROP INDEX IF EXISTS ""IX_semen_analyses_PatientId"";
                DROP INDEX IF EXISTS ""IX_semen_analyses_CycleId"";
                DROP INDEX IF EXISTS ""IX_semen_analyses_AnalysisDate"";

                -- Create partitioned table
                CREATE TABLE semen_analyses (
                    ""Id"" uuid NOT NULL,
                    ""PatientId"" uuid NOT NULL,
                    ""CycleId"" uuid,
                    ""AnalysisDate"" timestamp with time zone NOT NULL,
                    ""AnalysisType"" character varying(30) NOT NULL,
                    ""Volume"" numeric(10,2),
                    ""Appearance"" text,
                    ""Liquefaction"" text,
                    ""Ph"" numeric(4,2),
                    ""Concentration"" numeric(10,2),
                    ""TotalCount"" numeric(10,2),
                    ""ProgressiveMotility"" numeric(5,2),
                    ""NonProgressiveMotility"" numeric(5,2),
                    ""Immotile"" numeric(5,2),
                    ""NormalMorphology"" numeric(5,2),
                    ""Vitality"" numeric(5,2),
                    ""PostWashConcentration"" numeric(10,2),
                    ""PostWashMotility"" numeric(5,2),
                    ""Notes"" text,
                    ""PerformedByUserId"" uuid,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""UpdatedAt"" timestamp with time zone,
                    ""IsDeleted"" boolean NOT NULL DEFAULT false,
                    CONSTRAINT ""PK_semen_analyses"" PRIMARY KEY (""Id"", ""AnalysisDate"")
                ) PARTITION BY RANGE (""AnalysisDate"");

                -- Create monthly partitions (2025-2027) BEFORE copying data
                DO $$
                DECLARE
                    start_date DATE := '2025-01-01';
                    end_date DATE;
                    partition_name TEXT;
                BEGIN
                    FOR i IN 0..35 LOOP
                        end_date := start_date + INTERVAL '1 month';
                        partition_name := 'semen_analyses_' || TO_CHAR(start_date, 'YYYY_MM');
                        EXECUTE format(
                            'CREATE TABLE IF NOT EXISTS %I PARTITION OF semen_analyses FOR VALUES FROM (%L) TO (%L)',
                            partition_name, start_date, end_date
                        );
                        start_date := end_date;
                    END LOOP;
                END $$;

                CREATE TABLE IF NOT EXISTS semen_analyses_default PARTITION OF semen_analyses DEFAULT;

                -- Copy data
                INSERT INTO semen_analyses SELECT * FROM semen_analyses_old;

                -- Drop old table
                DROP TABLE semen_analyses_old;

                -- Recreate indexes
                CREATE INDEX ""IX_semen_analyses_PatientId"" ON semen_analyses (""PatientId"");
                CREATE INDEX ""IX_semen_analyses_CycleId"" ON semen_analyses (""CycleId"");
                CREATE INDEX ""IX_semen_analyses_AnalysisDate"" ON semen_analyses (""AnalysisDate"");
                CREATE INDEX ""IX_semen_analyses_IsDeleted"" ON semen_analyses (""IsDeleted"") WHERE ""IsDeleted"" = false;

                -- Recreate FKs
                ALTER TABLE semen_analyses ADD CONSTRAINT ""FK_semen_analyses_Patients_PatientId""
                    FOREIGN KEY (""PatientId"") REFERENCES patients (""Id"") ON DELETE CASCADE;
                ALTER TABLE semen_analyses ADD CONSTRAINT ""FK_semen_analyses_treatment_cycles_CycleId""
                    FOREIGN KEY (""CycleId"") REFERENCES treatment_cycles (""Id"") ON DELETE SET NULL;
            ");

            // =================================================================
            // 5. ServiceCatalogs — LIST partition by Category
            // =================================================================
            migrationBuilder.Sql(@"
                -- Rename old table and its PK constraint
                ALTER TABLE ""ServiceCatalogs"" RENAME TO ""ServiceCatalogs_old"";
                ALTER TABLE ""ServiceCatalogs_old"" RENAME CONSTRAINT ""PK_ServiceCatalogs"" TO ""PK_ServiceCatalogs_old"";

                -- Drop indexes
                DROP INDEX IF EXISTS ""IX_ServiceCatalogs_Code"";
                DROP INDEX IF EXISTS ""IX_ServiceCatalogs_Category"";
                DROP INDEX IF EXISTS ""IX_ServiceCatalogs_IsActive"";

                -- Create partitioned table
                CREATE TABLE ""ServiceCatalogs"" (
                    ""Id"" uuid NOT NULL,
                    ""Code"" character varying(20) NOT NULL,
                    ""Name"" character varying(200) NOT NULL,
                    ""Category"" character varying(50) NOT NULL,
                    ""UnitPrice"" numeric(18,0) NOT NULL,
                    ""Unit"" character varying(50) NOT NULL DEFAULT 'lần',
                    ""Description"" character varying(500),
                    ""IsActive"" boolean NOT NULL DEFAULT true,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""UpdatedAt"" timestamp with time zone,
                    ""IsDeleted"" boolean NOT NULL DEFAULT false,
                    CONSTRAINT ""PK_ServiceCatalogs"" PRIMARY KEY (""Id"", ""Category"")
                ) PARTITION BY LIST (""Category"");

                -- Create partitions for each ServiceCategory enum value BEFORE copying data
                CREATE TABLE ""ServiceCatalogs_LabTest"" PARTITION OF ""ServiceCatalogs"" FOR VALUES IN ('LabTest');
                CREATE TABLE ""ServiceCatalogs_Ultrasound"" PARTITION OF ""ServiceCatalogs"" FOR VALUES IN ('Ultrasound');
                CREATE TABLE ""ServiceCatalogs_Procedure"" PARTITION OF ""ServiceCatalogs"" FOR VALUES IN ('Procedure');
                CREATE TABLE ""ServiceCatalogs_Medication"" PARTITION OF ""ServiceCatalogs"" FOR VALUES IN ('Medication');
                CREATE TABLE ""ServiceCatalogs_Consultation"" PARTITION OF ""ServiceCatalogs"" FOR VALUES IN ('Consultation');
                CREATE TABLE ""ServiceCatalogs_IVF"" PARTITION OF ""ServiceCatalogs"" FOR VALUES IN ('IVF');
                CREATE TABLE ""ServiceCatalogs_Andrology"" PARTITION OF ""ServiceCatalogs"" FOR VALUES IN ('Andrology');
                CREATE TABLE ""ServiceCatalogs_SpermBank"" PARTITION OF ""ServiceCatalogs"" FOR VALUES IN ('SpermBank');
                CREATE TABLE ""ServiceCatalogs_Other"" PARTITION OF ""ServiceCatalogs"" FOR VALUES IN ('Other');

                -- Default partition for future categories
                CREATE TABLE ""ServiceCatalogs_default"" PARTITION OF ""ServiceCatalogs"" DEFAULT;

                -- Copy data
                INSERT INTO ""ServiceCatalogs"" SELECT * FROM ""ServiceCatalogs_old"";

                -- Drop old table
                DROP TABLE ""ServiceCatalogs_old"";

                -- Recreate indexes
                CREATE UNIQUE INDEX ""IX_ServiceCatalogs_Code"" ON ""ServiceCatalogs"" (""Code"", ""Category"");
                CREATE INDEX ""IX_ServiceCatalogs_Category"" ON ""ServiceCatalogs"" (""Category"");
                CREATE INDEX ""IX_ServiceCatalogs_IsActive"" ON ""ServiceCatalogs"" (""IsActive"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert ServiceCatalogs
            migrationBuilder.Sql(@"
                CREATE TABLE ""ServiceCatalogs_plain"" (LIKE ""ServiceCatalogs"" INCLUDING ALL);
                ALTER TABLE ""ServiceCatalogs_plain"" DROP CONSTRAINT IF EXISTS ""PK_ServiceCatalogs"";
                ALTER TABLE ""ServiceCatalogs_plain"" ADD PRIMARY KEY (""Id"");
                INSERT INTO ""ServiceCatalogs_plain"" SELECT * FROM ""ServiceCatalogs"";
                DROP TABLE ""ServiceCatalogs"" CASCADE;
                ALTER TABLE ""ServiceCatalogs_plain"" RENAME TO ""ServiceCatalogs"";
                CREATE UNIQUE INDEX ""IX_ServiceCatalogs_Code"" ON ""ServiceCatalogs"" (""Code"");
            ");

            // Revert semen_analyses
            migrationBuilder.Sql(@"
                CREATE TABLE semen_analyses_plain (LIKE semen_analyses INCLUDING ALL);
                ALTER TABLE semen_analyses_plain DROP CONSTRAINT IF EXISTS ""PK_semen_analyses"";
                ALTER TABLE semen_analyses_plain ADD PRIMARY KEY (""Id"");
                INSERT INTO semen_analyses_plain SELECT * FROM semen_analyses;
                DROP TABLE semen_analyses CASCADE;
                ALTER TABLE semen_analyses_plain RENAME TO semen_analyses;
            ");

            // Revert FormFieldValueDetails
            migrationBuilder.Sql(@"
                CREATE TABLE ""FormFieldValueDetails_plain"" (LIKE ""FormFieldValueDetails"" INCLUDING ALL);
                ALTER TABLE ""FormFieldValueDetails_plain"" DROP CONSTRAINT IF EXISTS ""PK_FormFieldValueDetails"";
                ALTER TABLE ""FormFieldValueDetails_plain"" ADD PRIMARY KEY (""Id"");
                INSERT INTO ""FormFieldValueDetails_plain"" SELECT * FROM ""FormFieldValueDetails"";
                DROP TABLE ""FormFieldValueDetails"" CASCADE;
                ALTER TABLE ""FormFieldValueDetails_plain"" RENAME TO ""FormFieldValueDetails"";
            ");

            // Revert form_field_values
            migrationBuilder.Sql(@"
                CREATE TABLE form_field_values_plain (LIKE form_field_values INCLUDING ALL);
                ALTER TABLE form_field_values_plain DROP CONSTRAINT IF EXISTS ""PK_form_field_values"";
                ALTER TABLE form_field_values_plain ADD PRIMARY KEY (""Id"");
                INSERT INTO form_field_values_plain SELECT * FROM form_field_values;
                DROP TABLE form_field_values CASCADE;
                ALTER TABLE form_field_values_plain RENAME TO form_field_values;
                CREATE UNIQUE INDEX ""IX_form_field_values_FormResponseId_FormFieldId""
                    ON form_field_values (""FormResponseId"", ""FormFieldId"");
            ");

            // Revert form_responses
            migrationBuilder.Sql(@"
                CREATE TABLE form_responses_plain (LIKE form_responses INCLUDING ALL);
                ALTER TABLE form_responses_plain DROP CONSTRAINT IF EXISTS ""PK_form_responses"";
                ALTER TABLE form_responses_plain ADD PRIMARY KEY (""Id"");
                INSERT INTO form_responses_plain SELECT * FROM form_responses;
                DROP TABLE form_responses CASCADE;
                ALTER TABLE form_responses_plain RENAME TO form_responses;
            ");

            // Revert FK change
            migrationBuilder.DropForeignKey(
                name: "FK_FormFieldValueDetails_form_field_values_FormFieldValueId",
                table: "FormFieldValueDetails");

            migrationBuilder.AddForeignKey(
                name: "FK_FormFieldValueDetails_form_field_values_FormFieldValueId",
                table: "FormFieldValueDetails",
                column: "FormFieldValueId",
                principalTable: "form_field_values",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
