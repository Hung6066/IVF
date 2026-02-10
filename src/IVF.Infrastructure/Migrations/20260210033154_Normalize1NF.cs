using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Normalize1NF : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Step 1: Create new child tables FIRST ──

            migrationBuilder.CreateTable(
                name: "birth_outcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BirthDataId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: true),
                    IsLiveBirth = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_birth_outcomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_birth_outcomes_birth_data_BirthDataId",
                        column: x => x.BirthDataId,
                        principalTable: "birth_data",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "luteal_phase_drugs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LutealPhaseDataId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    DrugName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_luteal_phase_drugs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_luteal_phase_drugs_luteal_phase_data_LutealPhaseDataId",
                        column: x => x.LutealPhaseDataId,
                        principalTable: "luteal_phase_data",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "queue_ticket_services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QueueTicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceCatalogId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_queue_ticket_services", x => x.Id);
                    table.ForeignKey(
                        name: "FK_queue_ticket_services_queue_tickets_QueueTicketId",
                        column: x => x.QueueTicketId,
                        principalTable: "queue_tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    // No FK to service_catalogs — partitioned table requires PK to include partition key
                });

            migrationBuilder.CreateTable(
                name: "stimulation_drugs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StimulationDataId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    DrugName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Duration = table.Column<int>(type: "integer", nullable: false),
                    Posology = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stimulation_drugs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stimulation_drugs_stimulation_data_StimulationDataId",
                        column: x => x.StimulationDataId,
                        principalTable: "stimulation_data",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_birth_outcomes_BirthDataId",
                table: "birth_outcomes",
                column: "BirthDataId");

            migrationBuilder.CreateIndex(
                name: "IX_luteal_phase_drugs_LutealPhaseDataId",
                table: "luteal_phase_drugs",
                column: "LutealPhaseDataId");

            migrationBuilder.CreateIndex(
                name: "IX_queue_ticket_services_QueueTicketId",
                table: "queue_ticket_services",
                column: "QueueTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_queue_ticket_services_QueueTicketId_ServiceCatalogId",
                table: "queue_ticket_services",
                columns: new[] { "QueueTicketId", "ServiceCatalogId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_queue_ticket_services_ServiceCatalogId",
                table: "queue_ticket_services",
                column: "ServiceCatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_stimulation_drugs_StimulationDataId",
                table: "stimulation_drugs",
                column: "StimulationDataId");

            // ── Step 2: Migrate existing data from old columns to new child tables ──

            // StimulationData Drug1-4 → stimulation_drugs
            migrationBuilder.Sql(@"
                INSERT INTO stimulation_drugs (""Id"", ""StimulationDataId"", ""SortOrder"", ""DrugName"", ""Duration"", ""Posology"", ""CreatedAt"", ""IsDeleted"")
                SELECT gen_random_uuid(), ""Id"", 0, ""Drug1"", COALESCE(""Drug1Duration"", 0), ""Drug1Posology"", NOW(), false
                FROM stimulation_data WHERE ""Drug1"" IS NOT NULL AND ""Drug1"" <> '';
                INSERT INTO stimulation_drugs (""Id"", ""StimulationDataId"", ""SortOrder"", ""DrugName"", ""Duration"", ""Posology"", ""CreatedAt"", ""IsDeleted"")
                SELECT gen_random_uuid(), ""Id"", 1, ""Drug2"", COALESCE(""Drug2Duration"", 0), ""Drug2Posology"", NOW(), false
                FROM stimulation_data WHERE ""Drug2"" IS NOT NULL AND ""Drug2"" <> '';
                INSERT INTO stimulation_drugs (""Id"", ""StimulationDataId"", ""SortOrder"", ""DrugName"", ""Duration"", ""Posology"", ""CreatedAt"", ""IsDeleted"")
                SELECT gen_random_uuid(), ""Id"", 2, ""Drug3"", COALESCE(""Drug3Duration"", 0), ""Drug3Posology"", NOW(), false
                FROM stimulation_data WHERE ""Drug3"" IS NOT NULL AND ""Drug3"" <> '';
                INSERT INTO stimulation_drugs (""Id"", ""StimulationDataId"", ""SortOrder"", ""DrugName"", ""Duration"", ""Posology"", ""CreatedAt"", ""IsDeleted"")
                SELECT gen_random_uuid(), ""Id"", 3, ""Drug4"", COALESCE(""Drug4Duration"", 0), ""Drug4Posology"", NOW(), false
                FROM stimulation_data WHERE ""Drug4"" IS NOT NULL AND ""Drug4"" <> '';
            ");

            // LutealPhaseData drugs → luteal_phase_drugs
            migrationBuilder.Sql(@"
                INSERT INTO luteal_phase_drugs (""Id"", ""LutealPhaseDataId"", ""SortOrder"", ""DrugName"", ""Category"", ""CreatedAt"", ""IsDeleted"")
                SELECT gen_random_uuid(), ""Id"", 0, ""LutealDrug1"", 'Luteal', NOW(), false
                FROM luteal_phase_data WHERE ""LutealDrug1"" IS NOT NULL AND ""LutealDrug1"" <> '';
                INSERT INTO luteal_phase_drugs (""Id"", ""LutealPhaseDataId"", ""SortOrder"", ""DrugName"", ""Category"", ""CreatedAt"", ""IsDeleted"")
                SELECT gen_random_uuid(), ""Id"", 1, ""LutealDrug2"", 'Luteal', NOW(), false
                FROM luteal_phase_data WHERE ""LutealDrug2"" IS NOT NULL AND ""LutealDrug2"" <> '';
                INSERT INTO luteal_phase_drugs (""Id"", ""LutealPhaseDataId"", ""SortOrder"", ""DrugName"", ""Category"", ""CreatedAt"", ""IsDeleted"")
                SELECT gen_random_uuid(), ""Id"", 2, ""EndometriumDrug1"", 'Endometrium', NOW(), false
                FROM luteal_phase_data WHERE ""EndometriumDrug1"" IS NOT NULL AND ""EndometriumDrug1"" <> '';
                INSERT INTO luteal_phase_drugs (""Id"", ""LutealPhaseDataId"", ""SortOrder"", ""DrugName"", ""Category"", ""CreatedAt"", ""IsDeleted"")
                SELECT gen_random_uuid(), ""Id"", 3, ""EndometriumDrug2"", 'Endometrium', NOW(), false
                FROM luteal_phase_data WHERE ""EndometriumDrug2"" IS NOT NULL AND ""EndometriumDrug2"" <> '';
            ");

            // BirthData BabyGenders/BirthWeights CSV → birth_outcomes
            migrationBuilder.Sql(@"
                INSERT INTO birth_outcomes (""Id"", ""BirthDataId"", ""SortOrder"", ""Gender"", ""Weight"", ""IsLiveBirth"", ""CreatedAt"", ""IsDeleted"")
                SELECT gen_random_uuid(), bd.""Id"", (row_number() OVER (PARTITION BY bd.""Id"" ORDER BY ordinality)) - 1,
                       trim(g.val), 
                       CASE WHEN trim(w.val) ~ '^\d+(\.\d+)?$' THEN trim(w.val)::numeric ELSE NULL END,
                       true, NOW(), false
                FROM birth_data bd,
                     unnest(string_to_array(bd.""BabyGenders"", ',')) WITH ORDINALITY AS g(val, ordinality)
                LEFT JOIN LATERAL (
                    SELECT val FROM unnest(string_to_array(bd.""BirthWeights"", ',')) WITH ORDINALITY AS w2(val, ord)
                    WHERE w2.ord = g.ordinality
                ) w ON true
                WHERE bd.""BabyGenders"" IS NOT NULL AND bd.""BabyGenders"" <> '';
            ");

            // QueueTicket ServiceIndications JSON → queue_ticket_services
            migrationBuilder.Sql(@"
                INSERT INTO queue_ticket_services (""Id"", ""QueueTicketId"", ""ServiceCatalogId"", ""CreatedAt"", ""IsDeleted"")
                SELECT gen_random_uuid(), qt.""Id"", elem::uuid, NOW(), false
                FROM queue_tickets qt,
                     jsonb_array_elements_text(qt.""ServiceIndications""::jsonb) AS elem
                WHERE qt.""ServiceIndications"" IS NOT NULL 
                  AND qt.""ServiceIndications"" <> ''
                  AND qt.""ServiceIndications"" <> '[]';
            ");

            // ── Step 3: Drop old columns after data is migrated ──

            migrationBuilder.DropColumn(name: "Drug1", table: "stimulation_data");
            migrationBuilder.DropColumn(name: "Drug1Duration", table: "stimulation_data");
            migrationBuilder.DropColumn(name: "Drug1Posology", table: "stimulation_data");
            migrationBuilder.DropColumn(name: "Drug2", table: "stimulation_data");
            migrationBuilder.DropColumn(name: "Drug2Duration", table: "stimulation_data");
            migrationBuilder.DropColumn(name: "Drug2Posology", table: "stimulation_data");
            migrationBuilder.DropColumn(name: "Drug3", table: "stimulation_data");
            migrationBuilder.DropColumn(name: "Drug3Duration", table: "stimulation_data");
            migrationBuilder.DropColumn(name: "Drug3Posology", table: "stimulation_data");
            migrationBuilder.DropColumn(name: "Drug4", table: "stimulation_data");
            migrationBuilder.DropColumn(name: "Drug4Duration", table: "stimulation_data");
            migrationBuilder.DropColumn(name: "Drug4Posology", table: "stimulation_data");
            migrationBuilder.DropColumn(name: "ServiceIndications", table: "queue_tickets");
            migrationBuilder.DropColumn(name: "EndometriumDrug1", table: "luteal_phase_data");
            migrationBuilder.DropColumn(name: "EndometriumDrug2", table: "luteal_phase_data");
            migrationBuilder.DropColumn(name: "LutealDrug1", table: "luteal_phase_data");
            migrationBuilder.DropColumn(name: "LutealDrug2", table: "luteal_phase_data");
            migrationBuilder.DropColumn(name: "BabyGenders", table: "birth_data");
            migrationBuilder.DropColumn(name: "BirthWeights", table: "birth_data");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "birth_outcomes");

            migrationBuilder.DropTable(
                name: "luteal_phase_drugs");

            migrationBuilder.DropTable(
                name: "queue_ticket_services");

            migrationBuilder.DropTable(
                name: "stimulation_drugs");

            migrationBuilder.AddColumn<string>(
                name: "Drug1",
                table: "stimulation_data",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Drug1Duration",
                table: "stimulation_data",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Drug1Posology",
                table: "stimulation_data",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Drug2",
                table: "stimulation_data",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Drug2Duration",
                table: "stimulation_data",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Drug2Posology",
                table: "stimulation_data",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Drug3",
                table: "stimulation_data",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Drug3Duration",
                table: "stimulation_data",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Drug3Posology",
                table: "stimulation_data",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Drug4",
                table: "stimulation_data",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Drug4Duration",
                table: "stimulation_data",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Drug4Posology",
                table: "stimulation_data",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceIndications",
                table: "queue_tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EndometriumDrug1",
                table: "luteal_phase_data",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EndometriumDrug2",
                table: "luteal_phase_data",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LutealDrug1",
                table: "luteal_phase_data",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LutealDrug2",
                table: "luteal_phase_data",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BabyGenders",
                table: "birth_data",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BirthWeights",
                table: "birth_data",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
