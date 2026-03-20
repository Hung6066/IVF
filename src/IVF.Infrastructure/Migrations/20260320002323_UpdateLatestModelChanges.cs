using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateLatestModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DispensedByUserId",
                table: "prescriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EnteredAt",
                table: "prescriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EnteredByUserId",
                table: "prescriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PrintedAt",
                table: "prescriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "prescriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "prescriptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "WaiveConsultationFee",
                table: "prescriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "consultations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsultationType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ConsultationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChiefComplaint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MedicalHistory = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PastHistory = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SurgicalHistory = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FamilyHistory = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ObstetricHistory = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MenstrualHistory = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PhysicalExamination = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Diagnosis = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TreatmentPlan = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RecommendedMethod = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    WaiveConsultationFee = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consultations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consultations_patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_consultations_treatment_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "treatment_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_consultations_users_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cycle_fees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsOneTimePerCycle = table.Column<bool>(type: "boolean", nullable: false),
                    WaivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WaivedReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WaivedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cycle_fees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cycle_fees_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cycle_fees_patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cycle_fees_treatment_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "treatment_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "drug_catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GenericName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActiveIngredient = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DefaultDosage = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drug_catalog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "egg_donors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DonorCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BloodType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Height = table.Column<decimal>(type: "numeric", nullable: true),
                    Weight = table.Column<decimal>(type: "numeric", nullable: true),
                    EyeColor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HairColor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Ethnicity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Education = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Occupation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ScreeningDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastDonationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalDonations = table.Column<int>(type: "integer", nullable: false),
                    SuccessfulPregnancies = table.Column<int>(type: "integer", nullable: false),
                    AmhLevel = table.Column<int>(type: "integer", nullable: true),
                    AntralFollicleCount = table.Column<int>(type: "integer", nullable: true),
                    MenstrualHistory = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MedicalHistory = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_egg_donors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_egg_donors_patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fet_protocols",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrepType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CycleDay = table.Column<int>(type: "integer", nullable: false),
                    EstrogenDrug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EstrogenDose = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EstrogenStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProgesteroneDrug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProgesteroneDose = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProgesteroneStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndometriumThickness = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    EndometriumPattern = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    EndometriumCheckDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EmbryosToThaw = table.Column<int>(type: "integer", nullable: false),
                    EmbryosSurvived = table.Column<int>(type: "integer", nullable: false),
                    ThawDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EmbryoGrade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EmbryoAge = table.Column<int>(type: "integer", nullable: false),
                    PlannedTransferDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fet_protocols", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fet_protocols_treatment_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "treatment_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "file_trackings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CurrentLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_trackings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_file_trackings_patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GenericName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Manufacturer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Supplier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CurrentStock = table.Column<int>(type: "integer", nullable: false),
                    MinStock = table.Column<int>(type: "integer", nullable: false),
                    MaxStock = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BatchNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StorageLocation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_requests_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_requests_users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lab_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrderType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ResultDeliveredTo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lab_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lab_orders_patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lab_orders_treatment_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "treatment_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lab_orders_users_OrderedByUserId",
                        column: x => x.OrderedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "medication_administrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrescriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdministeredByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MedicationCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Dosage = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Route = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Site = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AdministeredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsTriggerShot = table.Column<bool>(type: "boolean", nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medication_administrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_medication_administrations_patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_medication_administrations_prescriptions_PrescriptionId",
                        column: x => x.PrescriptionId,
                        principalTable: "prescriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_medication_administrations_treatment_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "treatment_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_medication_administrations_users_AdministeredByUserId",
                        column: x => x.AdministeredByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prescription_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CycleType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedByDoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prescription_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prescription_templates_doctors_CreatedByDoctorId",
                        column: x => x.CreatedByDoctorId,
                        principalTable: "doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "procedures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PerformedByDoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssistantDoctorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcedureType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProcedureCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ProcedureName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    AnesthesiaType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AnesthesiaNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RoomNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PreOpNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IntraOpFindings = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PostOpNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Complications = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_procedures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_procedures_doctors_AssistantDoctorId",
                        column: x => x.AssistantDoctorId,
                        principalTable: "doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_procedures_doctors_PerformedByDoctorId",
                        column: x => x.PerformedByDoctorId,
                        principalTable: "doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_procedures_patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_procedures_treatment_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "treatment_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "egg_donor_recipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EggDonorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientCoupleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_egg_donor_recipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_egg_donor_recipients_couples_RecipientCoupleId",
                        column: x => x.RecipientCoupleId,
                        principalTable: "couples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_egg_donor_recipients_egg_donors_EggDonorId",
                        column: x => x.EggDonorId,
                        principalTable: "egg_donors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_egg_donor_recipients_treatment_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "treatment_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_egg_donor_recipients_users_MatchedByUserId",
                        column: x => x.MatchedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "oocyte_samples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DonorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SampleCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CollectionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalOocytes = table.Column<int>(type: "integer", nullable: true),
                    MatureOocytes = table.Column<int>(type: "integer", nullable: true),
                    ImmatureOocytes = table.Column<int>(type: "integer", nullable: true),
                    DegeneratedOocytes = table.Column<int>(type: "integer", nullable: true),
                    VitrifiedCount = table.Column<int>(type: "integer", nullable: true),
                    CryoLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    FreezeDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ThawDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SurvivedAfterThaw = table.Column<int>(type: "integer", nullable: true),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oocyte_samples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_oocyte_samples_cryo_locations_CryoLocationId",
                        column: x => x.CryoLocationId,
                        principalTable: "cryo_locations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_oocyte_samples_egg_donors_DonorId",
                        column: x => x.DonorId,
                        principalTable: "egg_donors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "file_transfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileTrackingId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ToLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TransferredByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TransferredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_file_transfers_file_trackings_FileTrackingId",
                        column: x => x.FileTrackingId,
                        principalTable: "file_trackings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_file_transfers_users_TransferredByUserId",
                        column: x => x.TransferredByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    StockAfter = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PerformedByName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    BatchNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_transactions_inventory_items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "inventory_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lab_tests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LabOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TestCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TestName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ResultValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ResultUnit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReferenceRange = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsAbnormal = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lab_tests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lab_tests_lab_orders_LabOrderId",
                        column: x => x.LabOrderId,
                        principalTable: "lab_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prescription_template_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Dosage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Route = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Frequency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DurationDays = table.Column<int>(type: "integer", nullable: false),
                    Instructions = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prescription_template_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prescription_template_items_prescription_templates_Template~",
                        column: x => x.TemplateId,
                        principalTable: "prescription_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "consent_forms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcedureId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TemplateContent = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SignedByPatientId = table.Column<Guid>(type: "uuid", nullable: true),
                    PatientSignature = table.Column<string>(type: "text", nullable: true),
                    WitnessUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    WitnessSignature = table.Column<string>(type: "text", nullable: true),
                    DoctorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DoctorSignature = table.Column<string>(type: "text", nullable: true),
                    ScannedDocumentUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokeReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consent_forms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consent_forms_patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_consent_forms_procedures_ProcedureId",
                        column: x => x.ProcedureId,
                        principalTable: "procedures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_consent_forms_treatment_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "treatment_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prescriptions_TenantId",
                table: "prescriptions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_consent_forms_ConsentType",
                table: "consent_forms",
                column: "ConsentType");

            migrationBuilder.CreateIndex(
                name: "IX_consent_forms_CycleId",
                table: "consent_forms",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_consent_forms_PatientId",
                table: "consent_forms",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_consent_forms_ProcedureId",
                table: "consent_forms",
                column: "ProcedureId");

            migrationBuilder.CreateIndex(
                name: "IX_consent_forms_TenantId",
                table: "consent_forms",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_consultations_ConsultationDate",
                table: "consultations",
                column: "ConsultationDate");

            migrationBuilder.CreateIndex(
                name: "IX_consultations_CycleId",
                table: "consultations",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_consultations_DoctorId",
                table: "consultations",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_consultations_PatientId",
                table: "consultations",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_consultations_TenantId",
                table: "consultations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_cycle_fees_CycleId",
                table: "cycle_fees",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_cycle_fees_InvoiceId",
                table: "cycle_fees",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_cycle_fees_PatientId",
                table: "cycle_fees",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_cycle_fees_TenantId",
                table: "cycle_fees",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_drug_catalog_Category",
                table: "drug_catalog",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_drug_catalog_Code_TenantId",
                table: "drug_catalog",
                columns: new[] { "Code", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_drug_catalog_TenantId",
                table: "drug_catalog",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_egg_donor_recipients_CycleId",
                table: "egg_donor_recipients",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_egg_donor_recipients_EggDonorId",
                table: "egg_donor_recipients",
                column: "EggDonorId");

            migrationBuilder.CreateIndex(
                name: "IX_egg_donor_recipients_MatchedByUserId",
                table: "egg_donor_recipients",
                column: "MatchedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_egg_donor_recipients_RecipientCoupleId",
                table: "egg_donor_recipients",
                column: "RecipientCoupleId");

            migrationBuilder.CreateIndex(
                name: "IX_egg_donor_recipients_TenantId",
                table: "egg_donor_recipients",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_egg_donors_DonorCode",
                table: "egg_donors",
                column: "DonorCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_egg_donors_PatientId",
                table: "egg_donors",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_fet_protocols_CycleId",
                table: "fet_protocols",
                column: "CycleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fet_protocols_Status",
                table: "fet_protocols",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_fet_protocols_TenantId",
                table: "fet_protocols",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_file_trackings_FileCode_TenantId",
                table: "file_trackings",
                columns: new[] { "FileCode", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_file_trackings_PatientId",
                table: "file_trackings",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_file_trackings_TenantId",
                table: "file_trackings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_file_transfers_FileTrackingId",
                table: "file_transfers",
                column: "FileTrackingId");

            migrationBuilder.CreateIndex(
                name: "IX_file_transfers_TransferredByUserId",
                table: "file_transfers",
                column: "TransferredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_Category",
                table: "inventory_items",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_Code",
                table: "inventory_items",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_TenantId",
                table: "inventory_items",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_requests_ApprovedByUserId",
                table: "inventory_requests",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_requests_RequestedByUserId",
                table: "inventory_requests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_requests_Status",
                table: "inventory_requests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_requests_TenantId",
                table: "inventory_requests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_lab_orders_CycleId",
                table: "lab_orders",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_lab_orders_OrderedAt",
                table: "lab_orders",
                column: "OrderedAt");

            migrationBuilder.CreateIndex(
                name: "IX_lab_orders_OrderedByUserId",
                table: "lab_orders",
                column: "OrderedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_lab_orders_PatientId",
                table: "lab_orders",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_lab_orders_Status",
                table: "lab_orders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_lab_orders_TenantId",
                table: "lab_orders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_lab_tests_LabOrderId",
                table: "lab_tests",
                column: "LabOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_medication_administrations_AdministeredAt",
                table: "medication_administrations",
                column: "AdministeredAt");

            migrationBuilder.CreateIndex(
                name: "IX_medication_administrations_AdministeredByUserId",
                table: "medication_administrations",
                column: "AdministeredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_medication_administrations_CycleId",
                table: "medication_administrations",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_medication_administrations_PatientId",
                table: "medication_administrations",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_medication_administrations_PrescriptionId",
                table: "medication_administrations",
                column: "PrescriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_medication_administrations_TenantId",
                table: "medication_administrations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_oocyte_samples_CryoLocationId",
                table: "oocyte_samples",
                column: "CryoLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_oocyte_samples_DonorId",
                table: "oocyte_samples",
                column: "DonorId");

            migrationBuilder.CreateIndex(
                name: "IX_oocyte_samples_SampleCode",
                table: "oocyte_samples",
                column: "SampleCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prescription_template_items_TemplateId",
                table: "prescription_template_items",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_prescription_templates_CreatedByDoctorId",
                table: "prescription_templates",
                column: "CreatedByDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_prescription_templates_TenantId",
                table: "prescription_templates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_procedures_AssistantDoctorId",
                table: "procedures",
                column: "AssistantDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_procedures_CycleId",
                table: "procedures",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_procedures_PatientId",
                table: "procedures",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_procedures_PerformedByDoctorId",
                table: "procedures",
                column: "PerformedByDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_procedures_ScheduledAt",
                table: "procedures",
                column: "ScheduledAt");

            migrationBuilder.CreateIndex(
                name: "IX_procedures_Status",
                table: "procedures",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_procedures_TenantId",
                table: "procedures",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transactions_CreatedAt",
                table: "stock_transactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transactions_ItemId",
                table: "stock_transactions",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transactions_TenantId",
                table: "stock_transactions",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consent_forms");

            migrationBuilder.DropTable(
                name: "consultations");

            migrationBuilder.DropTable(
                name: "cycle_fees");

            migrationBuilder.DropTable(
                name: "drug_catalog");

            migrationBuilder.DropTable(
                name: "egg_donor_recipients");

            migrationBuilder.DropTable(
                name: "fet_protocols");

            migrationBuilder.DropTable(
                name: "file_transfers");

            migrationBuilder.DropTable(
                name: "inventory_requests");

            migrationBuilder.DropTable(
                name: "lab_tests");

            migrationBuilder.DropTable(
                name: "medication_administrations");

            migrationBuilder.DropTable(
                name: "oocyte_samples");

            migrationBuilder.DropTable(
                name: "prescription_template_items");

            migrationBuilder.DropTable(
                name: "stock_transactions");

            migrationBuilder.DropTable(
                name: "procedures");

            migrationBuilder.DropTable(
                name: "file_trackings");

            migrationBuilder.DropTable(
                name: "lab_orders");

            migrationBuilder.DropTable(
                name: "egg_donors");

            migrationBuilder.DropTable(
                name: "prescription_templates");

            migrationBuilder.DropTable(
                name: "inventory_items");

            migrationBuilder.DropIndex(
                name: "IX_prescriptions_TenantId",
                table: "prescriptions");

            migrationBuilder.DropColumn(
                name: "DispensedByUserId",
                table: "prescriptions");

            migrationBuilder.DropColumn(
                name: "EnteredAt",
                table: "prescriptions");

            migrationBuilder.DropColumn(
                name: "EnteredByUserId",
                table: "prescriptions");

            migrationBuilder.DropColumn(
                name: "PrintedAt",
                table: "prescriptions");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "prescriptions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "prescriptions");

            migrationBuilder.DropColumn(
                name: "WaiveConsultationFee",
                table: "prescriptions");
        }
    }
}
