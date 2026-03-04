using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRestricted",
                table: "patients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RestrictedAt",
                table: "patients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestrictionReason",
                table: "patients",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiBiasTestResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AiSystemName = table.Column<string>(type: "text", nullable: false),
                    TestType = table.Column<string>(type: "text", nullable: false),
                    ProtectedAttribute = table.Column<string>(type: "text", nullable: false),
                    ProtectedGroupValue = table.Column<string>(type: "text", nullable: false),
                    SampleSize = table.Column<int>(type: "integer", nullable: false),
                    TruePositives = table.Column<int>(type: "integer", nullable: false),
                    FalsePositives = table.Column<int>(type: "integer", nullable: false),
                    TrueNegatives = table.Column<int>(type: "integer", nullable: false),
                    FalseNegatives = table.Column<int>(type: "integer", nullable: false),
                    FalsePositiveRate = table.Column<decimal>(type: "numeric", nullable: false),
                    FalseNegativeRate = table.Column<decimal>(type: "numeric", nullable: false),
                    Accuracy = table.Column<decimal>(type: "numeric", nullable: false),
                    Precision = table.Column<decimal>(type: "numeric", nullable: false),
                    Recall = table.Column<decimal>(type: "numeric", nullable: false),
                    F1Score = table.Column<decimal>(type: "numeric", nullable: false),
                    BaselineFpr = table.Column<decimal>(type: "numeric", nullable: false),
                    BaselineFnr = table.Column<decimal>(type: "numeric", nullable: false),
                    DisparityRatioFpr = table.Column<decimal>(type: "numeric", nullable: false),
                    DisparityRatioFnr = table.Column<decimal>(type: "numeric", nullable: false),
                    PassesFairnessThreshold = table.Column<bool>(type: "boolean", nullable: false),
                    FairnessThreshold = table.Column<decimal>(type: "numeric", nullable: false),
                    FeatureImportance = table.Column<string>(type: "text", nullable: true),
                    Explanation = table.Column<string>(type: "text", nullable: true),
                    RemediationAction = table.Column<string>(type: "text", nullable: true),
                    TestRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TestRunBy = table.Column<string>(type: "text", nullable: true),
                    TestPeriodStart = table.Column<string>(type: "text", nullable: false),
                    TestPeriodEnd = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiBiasTestResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiModelVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AiSystemName = table.Column<string>(type: "text", nullable: false),
                    ModelVersion = table.Column<string>(type: "text", nullable: false),
                    PreviousVersion = table.Column<string>(type: "text", nullable: true),
                    ConfigurationJson = table.Column<string>(type: "text", nullable: false),
                    ThresholdsJson = table.Column<string>(type: "text", nullable: false),
                    FeatureSetJson = table.Column<string>(type: "text", nullable: false),
                    Accuracy = table.Column<double>(type: "double precision", nullable: true),
                    Precision = table.Column<double>(type: "double precision", nullable: true),
                    Recall = table.Column<double>(type: "double precision", nullable: true),
                    F1Score = table.Column<double>(type: "double precision", nullable: true),
                    Fpr = table.Column<double>(type: "double precision", nullable: true),
                    Fnr = table.Column<double>(type: "double precision", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ChangeDescription = table.Column<string>(type: "text", nullable: false),
                    ChangeReason = table.Column<string>(type: "text", nullable: true),
                    ApprovedBy = table.Column<string>(type: "text", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeployedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RollbackReason = table.Column<string>(type: "text", nullable: true),
                    BiasTestPassed = table.Column<bool>(type: "boolean", nullable: false),
                    BiasTestResultId = table.Column<Guid>(type: "uuid", nullable: true),
                    GitCommitHash = table.Column<string>(type: "text", nullable: true),
                    GitTag = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiModelVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssetInventories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetName = table.Column<string>(type: "text", nullable: false),
                    AssetType = table.Column<string>(type: "text", nullable: false),
                    Classification = table.Column<string>(type: "text", nullable: false),
                    Owner = table.Column<string>(type: "text", nullable: false),
                    Department = table.Column<string>(type: "text", nullable: true),
                    CriticalityLevel = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: true),
                    Environment = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    Hostname = table.Column<string>(type: "text", nullable: true),
                    ContainsPhi = table.Column<bool>(type: "boolean", nullable: false),
                    ContainsPii = table.Column<bool>(type: "boolean", nullable: false),
                    HasEncryption = table.Column<bool>(type: "boolean", nullable: false),
                    HasBackup = table.Column<bool>(type: "boolean", nullable: false),
                    HasAccessControl = table.Column<bool>(type: "boolean", nullable: false),
                    HasMonitoring = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Dependencies = table.Column<string>(type: "text", nullable: true),
                    SecurityControls = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    LastAuditedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextAuditDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecommissionedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetInventories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BreachNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uuid", nullable: false),
                    BreachType = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ContainedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotificationDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AffectedRecordCount = table.Column<int>(type: "integer", nullable: false),
                    AffectedDataTypes = table.Column<string>(type: "text", nullable: true),
                    AffectedSystems = table.Column<string>(type: "text", nullable: true),
                    AffectedUserIds = table.Column<string>(type: "text", nullable: true),
                    RootCause = table.Column<string>(type: "text", nullable: true),
                    AttackVector = table.Column<string>(type: "text", nullable: true),
                    MitreAttackId = table.Column<string>(type: "text", nullable: true),
                    DpaNotified = table.Column<bool>(type: "boolean", nullable: false),
                    DpaNotifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DpaReference = table.Column<string>(type: "text", nullable: true),
                    SubjectsNotified = table.Column<bool>(type: "boolean", nullable: false),
                    SubjectsNotifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubjectsNotifiedCount = table.Column<int>(type: "integer", nullable: false),
                    HhsNotified = table.Column<bool>(type: "boolean", nullable: false),
                    HhsNotifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MediaNotified = table.Column<bool>(type: "boolean", nullable: false),
                    MediaNotifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RemediationSteps = table.Column<string>(type: "text", nullable: true),
                    PreventionMeasures = table.Column<string>(type: "text", nullable: true),
                    AssignedTo = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LessonsLearned = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BreachNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Framework = table.Column<string>(type: "text", nullable: false),
                    Frequency = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Owner = table.Column<string>(type: "text", nullable: false),
                    AssignedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastCompletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastCompletionNotes = table.Column<string>(type: "text", nullable: true),
                    NextDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CompletionCount = table.Column<int>(type: "integer", nullable: false),
                    AutoReminder = table.Column<bool>(type: "boolean", nullable: false),
                    ReminderDaysBefore = table.Column<int>(type: "integer", nullable: false),
                    EvidenceRequired = table.Column<string>(type: "text", nullable: true),
                    RelatedDocumentId = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceTrainings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainingType = table.Column<string>(type: "text", nullable: false),
                    TrainingName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    ScorePercent = table.Column<int>(type: "integer", nullable: true),
                    IsPassed = table.Column<bool>(type: "boolean", nullable: false),
                    PassThreshold = table.Column<int>(type: "integer", nullable: false),
                    CertificateId = table.Column<string>(type: "text", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletionEvidence = table.Column<string>(type: "text", nullable: true),
                    AssignedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceTrainings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataSubjectRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestReference = table.Column<string>(type: "text", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: true),
                    DataSubjectName = table.Column<string>(type: "text", nullable: false),
                    DataSubjectEmail = table.Column<string>(type: "text", nullable: false),
                    RequestType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IdentityVerificationMethod = table.Column<string>(type: "text", nullable: true),
                    IdentityVerified = table.Column<bool>(type: "boolean", nullable: false),
                    IdentityVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IdentityVerifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExtendedDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExtensionReason = table.Column<string>(type: "text", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AssignedTo = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponseSummary = table.Column<string>(type: "text", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    AttachmentPaths = table.Column<string>(type: "text", nullable: true),
                    InternalNotes = table.Column<string>(type: "text", nullable: true),
                    LegalBasis = table.Column<string>(type: "text", nullable: true),
                    NotifiedDataSubject = table.Column<bool>(type: "boolean", nullable: false),
                    NotifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EscalatedToDpo = table.Column<bool>(type: "boolean", nullable: false),
                    EscalatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSubjectRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessingActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityName = table.Column<string>(type: "text", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    LegalBasis = table.Column<string>(type: "text", nullable: false),
                    DataCategories = table.Column<string>(type: "text", nullable: false),
                    DataSubjectCategories = table.Column<string>(type: "text", nullable: false),
                    ProcessingDescription = table.Column<string>(type: "text", nullable: true),
                    Recipients = table.Column<string>(type: "text", nullable: true),
                    ThirdCountryTransfers = table.Column<string>(type: "text", nullable: true),
                    RetentionPeriod = table.Column<string>(type: "text", nullable: false),
                    SecurityMeasures = table.Column<string>(type: "text", nullable: true),
                    RequiresDpia = table.Column<bool>(type: "boolean", nullable: false),
                    DpiaCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    DpiaCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DpiaReference = table.Column<string>(type: "text", nullable: true),
                    DataControllerName = table.Column<string>(type: "text", nullable: false),
                    DataControllerContact = table.Column<string>(type: "text", nullable: true),
                    DpoName = table.Column<string>(type: "text", nullable: true),
                    DpoContact = table.Column<string>(type: "text", nullable: true),
                    JointControllerDetails = table.Column<string>(type: "text", nullable: true),
                    ProcessorName = table.Column<string>(type: "text", nullable: true),
                    ProcessorContract = table.Column<string>(type: "text", nullable: true),
                    IsAutomatedDecisionMaking = table.Column<bool>(type: "boolean", nullable: false),
                    AutomatedDecisionDetails = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    LastReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextReviewDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingActivities", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiBiasTestResults");

            migrationBuilder.DropTable(
                name: "AiModelVersions");

            migrationBuilder.DropTable(
                name: "AssetInventories");

            migrationBuilder.DropTable(
                name: "BreachNotifications");

            migrationBuilder.DropTable(
                name: "ComplianceSchedules");

            migrationBuilder.DropTable(
                name: "ComplianceTrainings");

            migrationBuilder.DropTable(
                name: "DataSubjectRequests");

            migrationBuilder.DropTable(
                name: "ProcessingActivities");

            migrationBuilder.DropColumn(
                name: "IsRestricted",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "RestrictedAt",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "RestrictionReason",
                table: "patients");
        }
    }
}
