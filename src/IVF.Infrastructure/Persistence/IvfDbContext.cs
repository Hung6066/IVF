using IVF.Domain.Common;
using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Persistence;

public class IvfDbContext : DbContext
{
    private Guid? _currentTenantId;

    public IvfDbContext(DbContextOptions<IvfDbContext> options) : base(options)
    {
    }

    public void SetCurrentTenant(Guid tenantId) => _currentTenantId = tenantId;
    public Guid? CurrentTenantId => _currentTenantId;

    // Multi-tenancy
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<TenantUsageRecord> TenantUsageRecords => Set<TenantUsageRecord>();
    public DbSet<FeatureDefinition> FeatureDefinitions => Set<FeatureDefinition>();
    public DbSet<PlanDefinition> PlanDefinitions => Set<PlanDefinition>();
    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
    public DbSet<TenantFeature> TenantFeatures => Set<TenantFeature>();

    public DbSet<User> Users => Set<User>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<PatientPhoto> PatientPhotos => Set<PatientPhoto>();
    public DbSet<PatientFingerprint> PatientFingerprints => Set<PatientFingerprint>();
    public DbSet<Couple> Couples => Set<Couple>();
    public DbSet<TreatmentCycle> TreatmentCycles => Set<TreatmentCycle>();
    public DbSet<QueueTicket> QueueTickets => Set<QueueTicket>();
    public DbSet<Ultrasound> Ultrasounds => Set<Ultrasound>();
    public DbSet<Embryo> Embryos => Set<Embryo>();
    public DbSet<CryoLocation> CryoLocations => Set<CryoLocation>();
    public DbSet<SemenAnalysis> SemenAnalyses => Set<SemenAnalysis>();
    public DbSet<SpermWashing> SpermWashings => Set<SpermWashing>();
    public DbSet<SpermDonor> SpermDonors => Set<SpermDonor>();
    public DbSet<SpermSample> SpermSamples => Set<SpermSample>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ApiCallLog> ApiCallLogs => Set<ApiCallLog>();
    public DbSet<ServiceCatalog> ServiceCatalogs => Set<ServiceCatalog>();

    // Treatment Cycle Phase Data
    public DbSet<TreatmentIndication> TreatmentIndications => Set<TreatmentIndication>();
    public DbSet<StimulationData> StimulationData => Set<StimulationData>();
    public DbSet<StimulationDrug> StimulationDrugs => Set<StimulationDrug>();
    public DbSet<CultureData> CultureData => Set<CultureData>();
    public DbSet<TransferData> TransferData => Set<TransferData>();
    public DbSet<LutealPhaseData> LutealPhaseData => Set<LutealPhaseData>();
    public DbSet<LutealPhaseDrug> LutealPhaseDrugs => Set<LutealPhaseDrug>();
    public DbSet<PregnancyData> PregnancyData => Set<PregnancyData>();
    public DbSet<BirthData> BirthData => Set<BirthData>();
    public DbSet<BirthOutcome> BirthOutcomes => Set<BirthOutcome>();
    public DbSet<AdverseEventData> AdverseEventData => Set<AdverseEventData>();
    public DbSet<QueueTicketService> QueueTicketServices => Set<QueueTicketService>();

    // Dynamic Form Builder
    public DbSet<FormCategory> FormCategories => Set<FormCategory>();
    public DbSet<FormTemplate> FormTemplates => Set<FormTemplate>();
    public DbSet<FormField> FormFields => Set<FormField>();
    public DbSet<FormResponse> FormResponses => Set<FormResponse>();
    public DbSet<FormFieldValue> FormFieldValues => Set<FormFieldValue>();
    public DbSet<FormFieldValueDetail> FormFieldValueDetails => Set<FormFieldValueDetail>();
    public DbSet<FormFieldOption> FormFieldOptions => Set<FormFieldOption>();
    public DbSet<ReportTemplate> ReportTemplates => Set<ReportTemplate>();

    // Medical Concept Library
    public DbSet<Concept> Concepts => Set<Concept>();
    public DbSet<ConceptMapping> ConceptMappings => Set<ConceptMapping>();

    // Cross-Form Linked Data
    public DbSet<PatientConceptSnapshot> PatientConceptSnapshots => Set<PatientConceptSnapshot>();
    public DbSet<LinkedFieldSource> LinkedFieldSources => Set<LinkedFieldSource>();

    // Digital Signing
    public DbSet<UserSignature> UserSignatures => Set<UserSignature>();
    public DbSet<DocumentSignature> DocumentSignatures => Set<DocumentSignature>();
    public DbSet<SignedDocumentAmendment> SignedDocumentAmendments => Set<SignedDocumentAmendment>();
    public DbSet<AmendmentFieldChange> AmendmentFieldChanges => Set<AmendmentFieldChange>();

    // Patient Document Storage (MinIO S3)
    public DbSet<PatientDocument> PatientDocuments => Set<PatientDocument>();

    // Navigation Menu Configuration
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    // Permission Definitions (dynamic RBAC metadata)
    public DbSet<PermissionDefinition> PermissionDefinitions => Set<PermissionDefinition>();

    // Backup & Restore
    public DbSet<BackupOperation> BackupOperations => Set<BackupOperation>();
    public DbSet<BackupScheduleConfig> BackupScheduleConfigs => Set<BackupScheduleConfig>();
    public DbSet<CloudBackupConfig> CloudBackupConfigs => Set<CloudBackupConfig>();
    public DbSet<DataBackupStrategy> DataBackupStrategies => Set<DataBackupStrategy>();
    public DbSet<CloudReplicationConfig> CloudReplicationConfigs => Set<CloudReplicationConfig>();

    // Certificate Authority & mTLS
    public DbSet<CertificateAuthority> CertificateAuthorities => Set<CertificateAuthority>();
    public DbSet<ManagedCertificate> ManagedCertificates => Set<ManagedCertificate>();
    public DbSet<CertDeploymentLog> CertDeploymentLogs => Set<CertDeploymentLog>();
    public DbSet<CertificateRevocationList> CertificateRevocationLists => Set<CertificateRevocationList>();
    public DbSet<CertificateAuditEvent> CertificateAuditEvents => Set<CertificateAuditEvent>();

    // Security Events (Zero Trust continuous monitoring)
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();

    // Key Vault & Zero Trust
    public DbSet<ApiKeyManagement> ApiKeyManagements => Set<ApiKeyManagement>();
    public DbSet<DeviceRisk> DeviceRisks => Set<DeviceRisk>();
    public DbSet<ZTPolicy> ZTPolicies => Set<ZTPolicy>();

    // Advanced Security (Passkeys, MFA, Lockouts, IP Whitelist, Rate Limit, Geo Blocking)
    public DbSet<PasskeyCredential> PasskeyCredentials => Set<PasskeyCredential>();
    public DbSet<UserMfaSetting> UserMfaSettings => Set<UserMfaSetting>();
    public DbSet<AccountLockout> AccountLockouts => Set<AccountLockout>();
    public DbSet<IpWhitelistEntry> IpWhitelistEntries => Set<IpWhitelistEntry>();
    public DbSet<RateLimitConfig> RateLimitConfigs => Set<RateLimitConfig>();
    public DbSet<GeoBlockRule> GeoBlockRules => Set<GeoBlockRule>();

    // Enterprise User Management (Sessions, Groups, Analytics, Consent)
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<UserGroup> UserGroups => Set<UserGroup>();
    public DbSet<UserGroupMember> UserGroupMembers => Set<UserGroupMember>();
    public DbSet<UserGroupPermission> UserGroupPermissions => Set<UserGroupPermission>();
    public DbSet<UserLoginHistory> UserLoginHistories => Set<UserLoginHistory>();
    public DbSet<UserConsent> UserConsents => Set<UserConsent>();

    // Enterprise Security — Adaptive Auth, Threat Detection, Privacy, Delegation
    public DbSet<ConditionalAccessPolicy> ConditionalAccessPolicies => Set<ConditionalAccessPolicy>();
    public DbSet<UserBehaviorProfile> UserBehaviorProfiles => Set<UserBehaviorProfile>();
    public DbSet<SecurityIncident> SecurityIncidents => Set<SecurityIncident>();
    public DbSet<IncidentResponseRule> IncidentResponseRules => Set<IncidentResponseRule>();
    public DbSet<DataRetentionPolicy> DataRetentionPolicies => Set<DataRetentionPolicy>();
    public DbSet<UserExternalLogin> UserExternalLogins => Set<UserExternalLogin>();
    public DbSet<ImpersonationRequest> ImpersonationRequests => Set<ImpersonationRequest>();
    public DbSet<PermissionDelegation> PermissionDelegations => Set<PermissionDelegation>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    // Compliance — Breach Notification & Training Tracking
    public DbSet<BreachNotification> BreachNotifications => Set<BreachNotification>();
    public DbSet<ComplianceTraining> ComplianceTrainings => Set<ComplianceTraining>();

    // Compliance — Phase 2: Asset Inventory, ROPA, AI Bias Testing
    public DbSet<AssetInventory> AssetInventories => Set<AssetInventory>();
    public DbSet<ProcessingActivity> ProcessingActivities => Set<ProcessingActivity>();
    public DbSet<AiBiasTestResult> AiBiasTestResults => Set<AiBiasTestResult>();

    // Compliance — Phase 3: AI Model Versioning
    public DbSet<AiModelVersion> AiModelVersions => Set<AiModelVersion>();

    // Compliance — Phase 4: DSR Tracking, Compliance Schedule, Ongoing Monitoring
    public DbSet<DataSubjectRequest> DataSubjectRequests => Set<DataSubjectRequest>();
    public DbSet<ComplianceSchedule> ComplianceSchedules => Set<ComplianceSchedule>();

    // Vault System (self-hosted, Azure KV only for auto-unseal wrap/unwrap)
    public DbSet<VaultSecret> VaultSecrets => Set<VaultSecret>();
    public DbSet<VaultPolicy> VaultPolicies => Set<VaultPolicy>();
    public DbSet<VaultUserPolicy> VaultUserPolicies => Set<VaultUserPolicy>();
    public DbSet<VaultLease> VaultLeases => Set<VaultLease>();
    public DbSet<VaultDynamicCredential> VaultDynamicCredentials => Set<VaultDynamicCredential>();
    public DbSet<VaultToken> VaultTokens => Set<VaultToken>();
    public DbSet<VaultAutoUnseal> VaultAutoUnseals => Set<VaultAutoUnseal>();
    public DbSet<VaultSetting> VaultSettings => Set<VaultSetting>();
    public DbSet<VaultAuditLog> VaultAuditLogs => Set<VaultAuditLog>();
    public DbSet<EncryptionConfig> EncryptionConfigs => Set<EncryptionConfig>();
    public DbSet<FieldAccessPolicy> FieldAccessPolicies => Set<FieldAccessPolicy>();
    public DbSet<SecretRotationSchedule> SecretRotationSchedules => Set<SecretRotationSchedule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IvfDbContext).Assembly);

        // Apply tenant query filters to all ITenantEntity implementations
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(IvfDbContext)
                    .GetMethod(nameof(ApplyTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(this, [modelBuilder]);
            }
        }
    }

    private void ApplyTenantFilter<T>(ModelBuilder modelBuilder) where T : class, ITenantEntity
    {
        modelBuilder.Entity<T>().HasIndex(e => e.TenantId);
        modelBuilder.Entity<T>().HasQueryFilter(e => !EF.Property<bool>(e, "IsDeleted")
            && (_currentTenantId == null || e.TenantId == _currentTenantId));
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Modified
                && entry.Metadata.FindProperty("UpdatedAt") is not null)
            {
                entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
            }

            // Auto-set TenantId for new tenant entities
            if (entry.State == EntityState.Added
                && entry.Entity is ITenantEntity tenantEntity
                && tenantEntity.TenantId == Guid.Empty
                && _currentTenantId.HasValue)
            {
                tenantEntity.SetTenantId(_currentTenantId.Value);
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
