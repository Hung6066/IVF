using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Persistence.Migrations;

/// <summary>
/// Enterprise indexes for high-performance queries
/// Optimized for 10,000+ concurrent users
/// </summary>
public partial class AddEnterpriseIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ═══════════════════════════════════════════════════════════════════════
        // PATIENT INDEXES
        // ═══════════════════════════════════════════════════════════════════════

        // Composite index for tenant + MRN lookup (most common query)
        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_patients_tenant_mrn
            ON patients (tenant_id, mrn)
            WHERE is_deleted = false;
        ");

        // Full-text search index for patient name
        migrationBuilder.Sql(@"
            CREATE EXTENSION IF NOT EXISTS pg_trgm;

            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_patients_name_trgm
            ON patients USING gin (full_name gin_trgm_ops)
            WHERE is_deleted = false;
        ");

        // Phone number lookup
        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_patients_phone
            ON patients (phone)
            WHERE is_deleted = false AND phone IS NOT NULL;
        ");

        // Date of birth for age calculations
        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_patients_dob
            ON patients (tenant_id, date_of_birth)
            WHERE is_deleted = false;
        ");

        // ═══════════════════════════════════════════════════════════════════════
        // COUPLE INDEXES
        // ═══════════════════════════════════════════════════════════════════════

        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_couples_tenant_active
            ON couples (tenant_id, created_at DESC)
            WHERE is_deleted = false;
        ");

        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_couples_wife
            ON couples (wife_id)
            WHERE is_deleted = false;
        ");

        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_couples_husband
            ON couples (husband_id)
            WHERE is_deleted = false;
        ");

        // ═══════════════════════════════════════════════════════════════════════
        // CYCLE INDEXES
        // ═══════════════════════════════════════════════════════════════════════

        // Active cycles by tenant (dashboard query)
        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_cycles_tenant_status
            ON cycles (tenant_id, status, start_date DESC)
            WHERE is_deleted = false;
        ");

        // Cycles by couple (patient history)
        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_cycles_couple
            ON cycles (couple_id, start_date DESC)
            WHERE is_deleted = false;
        ");

        // Date range queries for reporting
        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_cycles_date_range
            ON cycles (tenant_id, start_date, end_date)
            WHERE is_deleted = false;
        ");

        // ═══════════════════════════════════════════════════════════════════════
        // EMBRYO INDEXES
        // ═══════════════════════════════════════════════════════════════════════

        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_embryos_cycle
            ON embryos (cycle_id, created_at DESC)
            WHERE is_deleted = false;
        ");

        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_embryos_status
            ON embryos (cycle_id, status)
            WHERE is_deleted = false;
        ");

        // ═══════════════════════════════════════════════════════════════════════
        // QUEUE INDEXES
        // ═══════════════════════════════════════════════════════════════════════

        // Active queue items (real-time display)
        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_queues_active
            ON queues (tenant_id, status, created_at)
            WHERE is_deleted = false AND status IN ('Waiting', 'Called', 'InProgress');
        ");

        // Queue by date for history
        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_queues_date
            ON queues (tenant_id, DATE(created_at), status)
            WHERE is_deleted = false;
        ");

        // ═══════════════════════════════════════════════════════════════════════
        // APPOINTMENT INDEXES
        // ═══════════════════════════════════════════════════════════════════════

        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_appointments_datetime
            ON appointments (tenant_id, appointment_date, appointment_time)
            WHERE is_deleted = false AND status != 'Cancelled';
        ");

        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_appointments_doctor
            ON appointments (doctor_id, appointment_date)
            WHERE is_deleted = false AND status != 'Cancelled';
        ");

        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_appointments_patient
            ON appointments (patient_id, appointment_date DESC)
            WHERE is_deleted = false;
        ");

        // ═══════════════════════════════════════════════════════════════════════
        // USER & SESSION INDEXES
        // ═══════════════════════════════════════════════════════════════════════

        // Username lookup (login)
        migrationBuilder.Sql(@"
            CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ix_users_username
            ON users (LOWER(username))
            WHERE is_deleted = false;
        ");

        // Email lookup
        migrationBuilder.Sql(@"
            CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ix_users_email
            ON users (LOWER(email))
            WHERE is_deleted = false AND email IS NOT NULL;
        ");

        // Active sessions by user
        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_sessions_user_active
            ON user_sessions (user_id, expires_at)
            WHERE is_active = true AND is_revoked = false;
        ");

        // ═══════════════════════════════════════════════════════════════════════
        // REFRESH TOKEN INDEXES
        // ═══════════════════════════════════════════════════════════════════════

        // Token hash lookup (validation)
        migrationBuilder.Sql(@"
            CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ix_refresh_tokens_hash
            ON refresh_tokens (token_hash)
            WHERE is_revoked = false;
        ");

        // Token family lookup (rotation detection)
        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_refresh_tokens_family
            ON refresh_tokens (family_id, created_at DESC);
        ");

        // ═══════════════════════════════════════════════════════════════════════
        // AUDIT LOG INDEXES
        // ═══════════════════════════════════════════════════════════════════════

        // Time-based queries (partitioned table)
        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_audit_logs_timestamp
            ON audit_logs (timestamp DESC, action);
        ");

        // User activity
        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_audit_logs_user
            ON audit_logs (user_id, timestamp DESC)
            WHERE user_id IS NOT NULL;
        ");

        // Resource tracking
        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_audit_logs_resource
            ON audit_logs (resource_type, resource_id, timestamp DESC);
        ");

        // ═══════════════════════════════════════════════════════════════════════
        // SECURITY EVENT INDEXES
        // ═══════════════════════════════════════════════════════════════════════

        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_security_events_timestamp
            ON security_events (tenant_id, timestamp DESC, severity);
        ");

        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_security_events_type
            ON security_events (event_type, timestamp DESC);
        ");

        // ═══════════════════════════════════════════════════════════════════════
        // IP WHITELIST & GEO BLOCKING INDEXES
        // ═══════════════════════════════════════════════════════════════════════

        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_ip_whitelist_active
            ON ip_whitelist_entries (tenant_id, is_active)
            WHERE is_deleted = false AND is_active = true;
        ");

        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_geo_block_rules_active
            ON geo_block_rules (tenant_id, country_code)
            WHERE is_deleted = false AND is_active = true;
        ");

        // ═══════════════════════════════════════════════════════════════════════
        // FORM & DOCUMENT INDEXES
        // ═══════════════════════════════════════════════════════════════════════

        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_forms_tenant_type
            ON forms (tenant_id, form_type, created_at DESC)
            WHERE is_deleted = false;
        ");

        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_documents_patient
            ON documents (patient_id, document_type, created_at DESC)
            WHERE is_deleted = false;
        ");

        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_documents_signed
            ON documents (tenant_id, is_signed, created_at DESC)
            WHERE is_deleted = false;
        ");

        // ═══════════════════════════════════════════════════════════════════════
        // BILLING INDEXES
        // ═══════════════════════════════════════════════════════════════════════

        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_invoices_patient
            ON invoices (patient_id, invoice_date DESC)
            WHERE is_deleted = false;
        ");

        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_invoices_status
            ON invoices (tenant_id, status, invoice_date DESC)
            WHERE is_deleted = false;
        ");

        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_payments_invoice
            ON payments (invoice_id, payment_date DESC);
        ");

        // ═══════════════════════════════════════════════════════════════════════
        // STATISTICS & ANALYTICS INDEXES
        // ═══════════════════════════════════════════════════════════════════════

        // Cycle outcome statistics
        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_cycles_outcome_stats
            ON cycles (tenant_id, outcome, DATE(start_date))
            WHERE is_deleted = false AND outcome IS NOT NULL;
        ");

        // Monthly aggregation support
        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_cycles_monthly
            ON cycles (tenant_id, date_trunc('month', start_date))
            WHERE is_deleted = false;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Drop all enterprise indexes
        var indexes = new[]
        {
            "ix_patients_tenant_mrn",
            "ix_patients_name_trgm",
            "ix_patients_phone",
            "ix_patients_dob",
            "ix_couples_tenant_active",
            "ix_couples_wife",
            "ix_couples_husband",
            "ix_cycles_tenant_status",
            "ix_cycles_couple",
            "ix_cycles_date_range",
            "ix_embryos_cycle",
            "ix_embryos_status",
            "ix_queues_active",
            "ix_queues_date",
            "ix_appointments_datetime",
            "ix_appointments_doctor",
            "ix_appointments_patient",
            "ix_users_username",
            "ix_users_email",
            "ix_sessions_user_active",
            "ix_refresh_tokens_hash",
            "ix_refresh_tokens_family",
            "ix_audit_logs_timestamp",
            "ix_audit_logs_user",
            "ix_audit_logs_resource",
            "ix_security_events_timestamp",
            "ix_security_events_type",
            "ix_ip_whitelist_active",
            "ix_geo_block_rules_active",
            "ix_forms_tenant_type",
            "ix_documents_patient",
            "ix_documents_signed",
            "ix_invoices_patient",
            "ix_invoices_status",
            "ix_payments_invoice",
            "ix_cycles_outcome_stats",
            "ix_cycles_monthly"
        };

        foreach (var index in indexes)
        {
            migrationBuilder.Sql($"DROP INDEX IF EXISTS {index};");
        }
    }
}
