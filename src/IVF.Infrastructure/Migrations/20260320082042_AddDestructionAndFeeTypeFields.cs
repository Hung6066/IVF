using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDestructionAndFeeTypeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DestroyedAt",
                table: "sperm_samples",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestroyedByUserId",
                table: "sperm_samples",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestroyedReason",
                table: "sperm_samples",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeeType",
                table: "invoice_items",
                type: "text",
                nullable: false,
                defaultValue: "IVFMD");

            migrationBuilder.CreateTable(
                name: "EmbryoFreezingContracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractNumber = table.Column<string>(type: "text", nullable: false),
                    ContractDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StorageStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StorageEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StorageDurationMonths = table.Column<int>(type: "integer", nullable: false),
                    AnnualFee = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalFeesPaid = table.Column<decimal>(type: "numeric", nullable: false),
                    LastPaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextPaymentDue = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TerminationReason = table.Column<string>(type: "text", nullable: true),
                    TerminatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TerminatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PatientSigned = table.Column<bool>(type: "boolean", nullable: false),
                    PatientSignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SignedDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmbryoFreezingContracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmbryoFreezingContracts_patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmbryoFreezingContracts_treatment_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "treatment_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EndometriumScans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    FetProtocolId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CycleDay = table.Column<int>(type: "integer", nullable: false),
                    ThicknessMm = table.Column<decimal>(type: "numeric", nullable: false),
                    Pattern = table.Column<string>(type: "text", nullable: true),
                    LengthMm = table.Column<decimal>(type: "numeric", nullable: true),
                    WidthMm = table.Column<decimal>(type: "numeric", nullable: true),
                    PolypsOrMyomata = table.Column<bool>(type: "boolean", nullable: false),
                    FluidInCavity = table.Column<string>(type: "text", nullable: true),
                    E2Level = table.Column<decimal>(type: "numeric", nullable: true),
                    LhLevel = table.Column<decimal>(type: "numeric", nullable: true),
                    P4Level = table.Column<decimal>(type: "numeric", nullable: true),
                    IsAdequate = table.Column<bool>(type: "boolean", nullable: false),
                    Recommendation = table.Column<string>(type: "text", nullable: true),
                    DoneByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EndometriumScans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EndometriumScans_fet_protocols_FetProtocolId",
                        column: x => x.FetProtocolId,
                        principalTable: "fet_protocols",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EndometriumScans_treatment_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "treatment_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpermSampleUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpermSampleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsageDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Procedure = table.Column<string>(type: "text", nullable: false),
                    VialsUsed = table.Column<int>(type: "integer", nullable: false),
                    AuthorizedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostThawMotility = table.Column<decimal>(type: "numeric", nullable: true),
                    PostThawConcentration = table.Column<int>(type: "integer", nullable: true),
                    PostThawNotes = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpermSampleUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpermSampleUsages_sperm_samples_SpermSampleId",
                        column: x => x.SpermSampleId,
                        principalTable: "sperm_samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SpermSampleUsages_treatment_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "treatment_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmbryoFreezingContracts_CycleId",
                table: "EmbryoFreezingContracts",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_EmbryoFreezingContracts_PatientId",
                table: "EmbryoFreezingContracts",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_EmbryoFreezingContracts_TenantId",
                table: "EmbryoFreezingContracts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EndometriumScans_CycleId",
                table: "EndometriumScans",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_EndometriumScans_FetProtocolId",
                table: "EndometriumScans",
                column: "FetProtocolId");

            migrationBuilder.CreateIndex(
                name: "IX_EndometriumScans_TenantId",
                table: "EndometriumScans",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SpermSampleUsages_CycleId",
                table: "SpermSampleUsages",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_SpermSampleUsages_SpermSampleId",
                table: "SpermSampleUsages",
                column: "SpermSampleId");

            migrationBuilder.CreateIndex(
                name: "IX_SpermSampleUsages_TenantId",
                table: "SpermSampleUsages",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmbryoFreezingContracts");

            migrationBuilder.DropTable(
                name: "EndometriumScans");

            migrationBuilder.DropTable(
                name: "SpermSampleUsages");

            migrationBuilder.DropColumn(
                name: "DestroyedAt",
                table: "sperm_samples");

            migrationBuilder.DropColumn(
                name: "DestroyedByUserId",
                table: "sperm_samples");

            migrationBuilder.DropColumn(
                name: "DestroyedReason",
                table: "sperm_samples");

            migrationBuilder.DropColumn(
                name: "FeeType",
                table: "invoice_items");
        }
    }
}
