using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCyclePhaseData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BetaHcg",
                table: "treatment_cycles",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CtrlNote",
                table: "treatment_cycles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EtIuiDoctor",
                table: "treatment_cycles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OpuNo",
                table: "treatment_cycles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PlannedCycleId",
                table: "treatment_cycles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Room",
                table: "treatment_cycles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StimulationNo",
                table: "treatment_cycles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StopReason",
                table: "treatment_cycles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransferNo",
                table: "treatment_cycles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TreatBothWifeAndEggDonor",
                table: "treatment_cycles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "adverse_event_data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Treatment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adverse_event_data", x => x.Id);
                    table.ForeignKey(
                        name: "FK_adverse_event_data_treatment_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "treatment_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "birth_data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GestationalWeeks = table.Column<int>(type: "integer", nullable: false),
                    DeliveryMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LiveBirths = table.Column<int>(type: "integer", nullable: false),
                    Stillbirths = table.Column<int>(type: "integer", nullable: false),
                    BabyGenders = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BirthWeights = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Complications = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_birth_data", x => x.Id);
                    table.ForeignKey(
                        name: "FK_birth_data_treatment_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "treatment_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "culture_data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalFreezedEmbryo = table.Column<int>(type: "integer", nullable: false),
                    TotalThawedEmbryo = table.Column<int>(type: "integer", nullable: false),
                    TotalTransferedEmbryo = table.Column<int>(type: "integer", nullable: false),
                    RemainFreezedEmbryo = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_culture_data", x => x.Id);
                    table.ForeignKey(
                        name: "FK_culture_data_treatment_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "treatment_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "luteal_phase_data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    LutealDrug1 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LutealDrug2 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EndometriumDrug1 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EndometriumDrug2 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_luteal_phase_data", x => x.Id);
                    table.ForeignKey(
                        name: "FK_luteal_phase_data_treatment_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "treatment_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pregnancy_data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    BetaHcg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    BetaHcgDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsPregnant = table.Column<bool>(type: "boolean", nullable: false),
                    GestationalSacs = table.Column<int>(type: "integer", nullable: true),
                    FetalHeartbeats = table.Column<int>(type: "integer", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pregnancy_data", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pregnancy_data_treatment_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "treatment_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stimulation_data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastMenstruation = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartDay = table.Column<int>(type: "integer", nullable: true),
                    Drug1 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Drug1Duration = table.Column<int>(type: "integer", nullable: false),
                    Drug1Posology = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Drug2 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Drug2Duration = table.Column<int>(type: "integer", nullable: false),
                    Drug2Posology = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Drug3 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Drug3Duration = table.Column<int>(type: "integer", nullable: false),
                    Drug3Posology = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Drug4 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Drug4Duration = table.Column<int>(type: "integer", nullable: false),
                    Drug4Posology = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Size12Follicle = table.Column<int>(type: "integer", nullable: true),
                    Size14Follicle = table.Column<int>(type: "integer", nullable: true),
                    EndometriumThickness = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    TriggerDrug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TriggerDrug2 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HcgDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HcgDate2 = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HcgTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    HcgTime2 = table.Column<TimeSpan>(type: "interval", nullable: true),
                    LhLab = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    E2Lab = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    P4Lab = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ProcedureType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AspirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcedureDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AspirationNo = table.Column<int>(type: "integer", nullable: true),
                    TechniqueWife = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TechniqueHusband = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stimulation_data", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stimulation_data_treatment_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "treatment_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transfer_data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ThawingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DayOfTransfered = table.Column<int>(type: "integer", nullable: false),
                    LabNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transfer_data", x => x.Id);
                    table.ForeignKey(
                        name: "FK_transfer_data_treatment_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "treatment_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "treatment_indications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastMenstruation = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TreatmentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Regimen = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FreezeAll = table.Column<bool>(type: "boolean", nullable: false),
                    Sis = table.Column<bool>(type: "boolean", nullable: false),
                    WifeDiagnosis = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WifeDiagnosis2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HusbandDiagnosis = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HusbandDiagnosis2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UltrasoundDoctorId = table.Column<Guid>(type: "uuid", nullable: true),
                    IndicationDoctorId = table.Column<Guid>(type: "uuid", nullable: true),
                    FshDoctorId = table.Column<Guid>(type: "uuid", nullable: true),
                    MidwifeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Timelapse = table.Column<bool>(type: "boolean", nullable: false),
                    PgtA = table.Column<bool>(type: "boolean", nullable: false),
                    PgtSr = table.Column<bool>(type: "boolean", nullable: false),
                    PgtM = table.Column<bool>(type: "boolean", nullable: false),
                    SubType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ScientificResearch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProcedurePlace = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StopReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TreatmentMonth = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PreviousTreatmentsAtSite = table.Column<int>(type: "integer", nullable: false),
                    PreviousTreatmentsOther = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_treatment_indications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_treatment_indications_treatment_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "treatment_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_adverse_event_data_CycleId",
                table: "adverse_event_data",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_birth_data_CycleId",
                table: "birth_data",
                column: "CycleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_culture_data_CycleId",
                table: "culture_data",
                column: "CycleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_luteal_phase_data_CycleId",
                table: "luteal_phase_data",
                column: "CycleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pregnancy_data_CycleId",
                table: "pregnancy_data",
                column: "CycleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stimulation_data_CycleId",
                table: "stimulation_data",
                column: "CycleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transfer_data_CycleId",
                table: "transfer_data",
                column: "CycleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_treatment_indications_CycleId",
                table: "treatment_indications",
                column: "CycleId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "adverse_event_data");

            migrationBuilder.DropTable(
                name: "birth_data");

            migrationBuilder.DropTable(
                name: "culture_data");

            migrationBuilder.DropTable(
                name: "luteal_phase_data");

            migrationBuilder.DropTable(
                name: "pregnancy_data");

            migrationBuilder.DropTable(
                name: "stimulation_data");

            migrationBuilder.DropTable(
                name: "transfer_data");

            migrationBuilder.DropTable(
                name: "treatment_indications");

            migrationBuilder.DropColumn(
                name: "BetaHcg",
                table: "treatment_cycles");

            migrationBuilder.DropColumn(
                name: "CtrlNote",
                table: "treatment_cycles");

            migrationBuilder.DropColumn(
                name: "EtIuiDoctor",
                table: "treatment_cycles");

            migrationBuilder.DropColumn(
                name: "OpuNo",
                table: "treatment_cycles");

            migrationBuilder.DropColumn(
                name: "PlannedCycleId",
                table: "treatment_cycles");

            migrationBuilder.DropColumn(
                name: "Room",
                table: "treatment_cycles");

            migrationBuilder.DropColumn(
                name: "StimulationNo",
                table: "treatment_cycles");

            migrationBuilder.DropColumn(
                name: "StopReason",
                table: "treatment_cycles");

            migrationBuilder.DropColumn(
                name: "TransferNo",
                table: "treatment_cycles");

            migrationBuilder.DropColumn(
                name: "TreatBothWifeAndEggDonor",
                table: "treatment_cycles");
        }
    }
}
