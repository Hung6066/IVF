using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNewEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Prescription_patients_PatientId",
                table: "Prescription");

            migrationBuilder.DropForeignKey(
                name: "FK_Prescription_treatment_cycles_CycleId",
                table: "Prescription");

            migrationBuilder.DropForeignKey(
                name: "FK_Prescription_users_DoctorId",
                table: "Prescription");

            migrationBuilder.DropForeignKey(
                name: "FK_PrescriptionItem_Prescription_PrescriptionId",
                table: "PrescriptionItem");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PrescriptionItem",
                table: "PrescriptionItem");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Prescription",
                table: "Prescription");

            migrationBuilder.RenameTable(
                name: "PrescriptionItem",
                newName: "prescription_items");

            migrationBuilder.RenameTable(
                name: "Prescription",
                newName: "prescriptions");

            migrationBuilder.RenameIndex(
                name: "IX_PrescriptionItem_PrescriptionId",
                table: "prescription_items",
                newName: "IX_prescription_items_PrescriptionId");

            migrationBuilder.RenameIndex(
                name: "IX_Prescription_PatientId",
                table: "prescriptions",
                newName: "IX_prescriptions_PatientId");

            migrationBuilder.RenameIndex(
                name: "IX_Prescription_DoctorId",
                table: "prescriptions",
                newName: "IX_prescriptions_DoctorId");

            migrationBuilder.RenameIndex(
                name: "IX_Prescription_CycleId",
                table: "prescriptions",
                newName: "IX_prescriptions_CycleId");

            migrationBuilder.AlterColumn<string>(
                name: "Frequency",
                table: "prescription_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Duration",
                table: "prescription_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DrugName",
                table: "prescription_items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "DrugCode",
                table: "prescription_items",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Dosage",
                table: "prescription_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "prescriptions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "prescriptions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TreatmentCycleId",
                table: "prescriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_prescription_items",
                table: "prescription_items",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_prescriptions",
                table: "prescriptions",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_invoices_patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_invoices_treatment_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "treatment_cycles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "semen_analyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnalysisDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AnalysisType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Volume = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Appearance = table.Column<string>(type: "text", nullable: true),
                    Liquefaction = table.Column<string>(type: "text", nullable: true),
                    Ph = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    Concentration = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    TotalCount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ProgressiveMotility = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    NonProgressiveMotility = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Immotile = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    NormalMorphology = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Vitality = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    PostWashConcentration = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    PostWashMotility = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_semen_analyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_semen_analyses_patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_semen_analyses_treatment_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "treatment_cycles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "sperm_donors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DonorCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BloodType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Height = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Weight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    EyeColor = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    HairColor = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Ethnicity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Education = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Occupation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ScreeningDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastDonationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalDonations = table.Column<int>(type: "integer", nullable: false),
                    SuccessfulPregnancies = table.Column<int>(type: "integer", nullable: false),
                    MedicalHistory = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sperm_donors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sperm_donors_patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invoice_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_invoice_items_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TransactionReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ReceivedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payments_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sperm_samples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DonorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SampleCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CollectionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SpecimenType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Volume = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Concentration = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Motility = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    VialCount = table.Column<int>(type: "integer", nullable: true),
                    CryoLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    FreezeDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ThawDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sperm_samples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sperm_samples_cryo_locations_CryoLocationId",
                        column: x => x.CryoLocationId,
                        principalTable: "cryo_locations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_sperm_samples_sperm_donors_DonorId",
                        column: x => x.DonorId,
                        principalTable: "sperm_donors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prescriptions_PrescriptionDate",
                table: "prescriptions",
                column: "PrescriptionDate");

            migrationBuilder.CreateIndex(
                name: "IX_prescriptions_TreatmentCycleId",
                table: "prescriptions",
                column: "TreatmentCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_InvoiceId",
                table: "invoice_items",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_CycleId",
                table: "invoices",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_InvoiceDate",
                table: "invoices",
                column: "InvoiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_InvoiceNumber",
                table: "invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_PatientId",
                table: "invoices",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_Status",
                table: "invoices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_payments_InvoiceId",
                table: "payments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_PaymentDate",
                table: "payments",
                column: "PaymentDate");

            migrationBuilder.CreateIndex(
                name: "IX_payments_PaymentNumber",
                table: "payments",
                column: "PaymentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_semen_analyses_AnalysisDate",
                table: "semen_analyses",
                column: "AnalysisDate");

            migrationBuilder.CreateIndex(
                name: "IX_semen_analyses_CycleId",
                table: "semen_analyses",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_semen_analyses_PatientId",
                table: "semen_analyses",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_sperm_donors_DonorCode",
                table: "sperm_donors",
                column: "DonorCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sperm_donors_PatientId",
                table: "sperm_donors",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_sperm_donors_Status",
                table: "sperm_donors",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_sperm_samples_CryoLocationId",
                table: "sperm_samples",
                column: "CryoLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_sperm_samples_DonorId",
                table: "sperm_samples",
                column: "DonorId");

            migrationBuilder.CreateIndex(
                name: "IX_sperm_samples_IsAvailable",
                table: "sperm_samples",
                column: "IsAvailable");

            migrationBuilder.CreateIndex(
                name: "IX_sperm_samples_SampleCode",
                table: "sperm_samples",
                column: "SampleCode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_prescription_items_prescriptions_PrescriptionId",
                table: "prescription_items",
                column: "PrescriptionId",
                principalTable: "prescriptions",
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
                name: "FK_prescriptions_treatment_cycles_TreatmentCycleId",
                table: "prescriptions",
                column: "TreatmentCycleId",
                principalTable: "treatment_cycles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_prescriptions_users_DoctorId",
                table: "prescriptions",
                column: "DoctorId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_prescription_items_prescriptions_PrescriptionId",
                table: "prescription_items");

            migrationBuilder.DropForeignKey(
                name: "FK_prescriptions_patients_PatientId",
                table: "prescriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_prescriptions_treatment_cycles_CycleId",
                table: "prescriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_prescriptions_treatment_cycles_TreatmentCycleId",
                table: "prescriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_prescriptions_users_DoctorId",
                table: "prescriptions");

            migrationBuilder.DropTable(
                name: "invoice_items");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "semen_analyses");

            migrationBuilder.DropTable(
                name: "sperm_samples");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "sperm_donors");

            migrationBuilder.DropPrimaryKey(
                name: "PK_prescriptions",
                table: "prescriptions");

            migrationBuilder.DropIndex(
                name: "IX_prescriptions_PrescriptionDate",
                table: "prescriptions");

            migrationBuilder.DropIndex(
                name: "IX_prescriptions_TreatmentCycleId",
                table: "prescriptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_prescription_items",
                table: "prescription_items");

            migrationBuilder.DropColumn(
                name: "TreatmentCycleId",
                table: "prescriptions");

            migrationBuilder.RenameTable(
                name: "prescriptions",
                newName: "Prescription");

            migrationBuilder.RenameTable(
                name: "prescription_items",
                newName: "PrescriptionItem");

            migrationBuilder.RenameIndex(
                name: "IX_prescriptions_PatientId",
                table: "Prescription",
                newName: "IX_Prescription_PatientId");

            migrationBuilder.RenameIndex(
                name: "IX_prescriptions_DoctorId",
                table: "Prescription",
                newName: "IX_Prescription_DoctorId");

            migrationBuilder.RenameIndex(
                name: "IX_prescriptions_CycleId",
                table: "Prescription",
                newName: "IX_Prescription_CycleId");

            migrationBuilder.RenameIndex(
                name: "IX_prescription_items_PrescriptionId",
                table: "PrescriptionItem",
                newName: "IX_PrescriptionItem_PrescriptionId");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Prescription",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "Prescription",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Frequency",
                table: "PrescriptionItem",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Duration",
                table: "PrescriptionItem",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DrugName",
                table: "PrescriptionItem",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "DrugCode",
                table: "PrescriptionItem",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Dosage",
                table: "PrescriptionItem",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Prescription",
                table: "Prescription",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PrescriptionItem",
                table: "PrescriptionItem",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Prescription_patients_PatientId",
                table: "Prescription",
                column: "PatientId",
                principalTable: "patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Prescription_treatment_cycles_CycleId",
                table: "Prescription",
                column: "CycleId",
                principalTable: "treatment_cycles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Prescription_users_DoctorId",
                table: "Prescription",
                column: "DoctorId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PrescriptionItem_Prescription_PrescriptionId",
                table: "PrescriptionItem",
                column: "PrescriptionId",
                principalTable: "Prescription",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
