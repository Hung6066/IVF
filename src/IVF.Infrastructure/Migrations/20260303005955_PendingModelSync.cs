using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PendingModelSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Allergies",
                table: "patients",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AnonymizedAt",
                table: "patients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BloodType",
                table: "patients",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ConsentDataProcessing",
                table: "patients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentDataProcessingDate",
                table: "patients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ConsentMarketing",
                table: "patients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentMarketingDate",
                table: "patients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ConsentResearch",
                table: "patients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentResearchDate",
                table: "patients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataRetentionExpiryDate",
                table: "patients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "patients",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactName",
                table: "patients",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactPhone",
                table: "patients",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactRelation",
                table: "patients",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ethnicity",
                table: "patients",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InsuranceNumber",
                table: "patients",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InsuranceProvider",
                table: "patients",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAnonymized",
                table: "patients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastVisitDate",
                table: "patients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MedicalNotes",
                table: "patients",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nationality",
                table: "patients",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "patients",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Occupation",
                table: "patients",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "patients",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.AddColumn<string>(
                name: "ReferralSource",
                table: "patients",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferringDoctorId",
                table: "patients",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiskLevel",
                table: "patients",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Low");

            migrationBuilder.AddColumn<string>(
                name: "RiskNotes",
                table: "patients",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "patients",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "patients",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalVisits",
                table: "patients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "UserConsents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsentType = table.Column<string>(type: "text", nullable: false),
                    IsGranted = table.Column<bool>(type: "boolean", nullable: false),
                    ConsentVersion = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    ConsentedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedReason = table.Column<string>(type: "text", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserConsents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserGroupMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberRole = table.Column<string>(type: "text", nullable: false),
                    AddedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGroupMembers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserGroupPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionCode = table.Column<string>(type: "text", nullable: false),
                    GrantedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGroupPermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    GroupType = table.Column<string>(type: "text", nullable: false),
                    ParentGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserLoginHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginMethod = table.Column<string>(type: "text", nullable: false),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    DeviceFingerprint = table.Column<string>(type: "text", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    DeviceType = table.Column<string>(type: "text", nullable: true),
                    OperatingSystem = table.Column<string>(type: "text", nullable: true),
                    Browser = table.Column<string>(type: "text", nullable: true),
                    RiskScore = table.Column<decimal>(type: "numeric", nullable: true),
                    IsSuspicious = table.Column<bool>(type: "boolean", nullable: false),
                    RiskFactors = table.Column<string>(type: "text", nullable: true),
                    SessionDuration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    LoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LogoutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLoginHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionToken = table.Column<string>(type: "text", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    DeviceFingerprint = table.Column<string>(type: "text", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    DeviceType = table.Column<string>(type: "text", nullable: true),
                    OperatingSystem = table.Column<string>(type: "text", nullable: true),
                    Browser = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    RevokedReason = table.Column<string>(type: "text", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_patients_CreatedAt",
                table: "patients",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_patients_DataRetentionExpiryDate_IsAnonymized",
                table: "patients",
                columns: new[] { "DataRetentionExpiryDate", "IsAnonymized" });

            migrationBuilder.CreateIndex(
                name: "IX_patients_Email",
                table: "patients",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_patients_InsuranceNumber",
                table: "patients",
                column: "InsuranceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_patients_LastVisitDate",
                table: "patients",
                column: "LastVisitDate");

            migrationBuilder.CreateIndex(
                name: "IX_patients_PatientType",
                table: "patients",
                column: "PatientType");

            migrationBuilder.CreateIndex(
                name: "IX_patients_Phone",
                table: "patients",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_patients_Priority",
                table: "patients",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_patients_RiskLevel",
                table: "patients",
                column: "RiskLevel");

            migrationBuilder.CreateIndex(
                name: "IX_patients_Status",
                table: "patients",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_patients_Status_LastVisitDate",
                table: "patients",
                columns: new[] { "Status", "LastVisitDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserConsents");

            migrationBuilder.DropTable(
                name: "UserGroupMembers");

            migrationBuilder.DropTable(
                name: "UserGroupPermissions");

            migrationBuilder.DropTable(
                name: "UserGroups");

            migrationBuilder.DropTable(
                name: "UserLoginHistories");

            migrationBuilder.DropTable(
                name: "UserSessions");

            migrationBuilder.DropIndex(
                name: "IX_patients_CreatedAt",
                table: "patients");

            migrationBuilder.DropIndex(
                name: "IX_patients_DataRetentionExpiryDate_IsAnonymized",
                table: "patients");

            migrationBuilder.DropIndex(
                name: "IX_patients_Email",
                table: "patients");

            migrationBuilder.DropIndex(
                name: "IX_patients_InsuranceNumber",
                table: "patients");

            migrationBuilder.DropIndex(
                name: "IX_patients_LastVisitDate",
                table: "patients");

            migrationBuilder.DropIndex(
                name: "IX_patients_PatientType",
                table: "patients");

            migrationBuilder.DropIndex(
                name: "IX_patients_Phone",
                table: "patients");

            migrationBuilder.DropIndex(
                name: "IX_patients_Priority",
                table: "patients");

            migrationBuilder.DropIndex(
                name: "IX_patients_RiskLevel",
                table: "patients");

            migrationBuilder.DropIndex(
                name: "IX_patients_Status",
                table: "patients");

            migrationBuilder.DropIndex(
                name: "IX_patients_Status_LastVisitDate",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "Allergies",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "AnonymizedAt",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "BloodType",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "ConsentDataProcessing",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "ConsentDataProcessingDate",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "ConsentMarketing",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "ConsentMarketingDate",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "ConsentResearch",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "ConsentResearchDate",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "DataRetentionExpiryDate",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "EmergencyContactName",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "EmergencyContactPhone",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "EmergencyContactRelation",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "Ethnicity",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "InsuranceNumber",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "InsuranceProvider",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "IsAnonymized",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "LastVisitDate",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "MedicalNotes",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "Nationality",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "Occupation",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "ReferralSource",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "ReferringDoctorId",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "RiskLevel",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "RiskNotes",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "TotalVisits",
                table: "patients");
        }
    }
}
