namespace IVF.API.Services;

/// <summary>
/// Evaluates 3-2-1 backup rule compliance:
/// 3 copies of data, 2 different storage types, 1 offsite copy.
/// </summary>
public sealed class BackupComplianceService(
    DatabaseBackupService dbBackupService,
    MinioBackupService minioBackupService,
    WalBackupService walBackupService,
    ReplicationMonitorService replicationService,
    BackupRestoreService pkiBackupService,
    CloudBackupProviderFactory cloudProviderFactory,
    IWebHostEnvironment env,
    ILogger<BackupComplianceService> logger)
{
    private string BackupsDir => Path.Combine(
        Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..")), "backups");

    public async Task<BackupComplianceReport> EvaluateAsync(CancellationToken ct = default)
    {
        var checks = new List<BackupComplianceCheck>();
        var recommendations = new List<string>();

        // ── 1. THREE COPIES ──────────────────────────────────────
        // Copy 1: Live database (always exists)
        var dbInfo = await dbBackupService.GetDatabaseInfoAsync(ct);
        checks.Add(new BackupComplianceCheck(
            "copy_live_database",
            "Bản gốc — Cơ sở dữ liệu đang hoạt động",
            dbInfo.Connected,
            dbInfo.Connected ? $"PostgreSQL '{dbInfo.DatabaseName}' đang chạy ({FormatSize(dbInfo.SizeBytes)})" : "Không thể kết nối đến PostgreSQL"
        ));

        // Copy 2: Local backups (pg_dump files)
        var dbBackups = dbBackupService.ListDatabaseBackups(BackupsDir);
        var minioBackups = minioBackupService.ListMinioBackups(BackupsDir);
        var hasLocalBackups = dbBackups.Count > 0;
        checks.Add(new BackupComplianceCheck(
            "copy_local_backup",
            "Bản sao cục bộ — Backup trên đĩa cục bộ",
            hasLocalBackups,
            hasLocalBackups
                ? $"{dbBackups.Count} bản DB + {minioBackups.Count} bản MinIO ({FormatSize(dbBackups.Sum(b => b.SizeBytes) + minioBackups.Sum(b => b.SizeBytes))})"
                : "Chưa có bản sao lưu cục bộ"
        ));
        if (!hasLocalBackups)
            recommendations.Add("Tạo ít nhất một bản sao lưu cơ sở dữ liệu (Chiến lược → Chạy ngay)");

        // Copy 3: Cloud/offsite backup
        bool hasCloudBackups = false;
        string cloudDetail = "Chưa cấu hình hoặc chưa tải lên cloud";
        try
        {
            var cloudProvider = await cloudProviderFactory.GetProviderAsync(ct);
            var cloudObjects = await cloudProvider.ListAsync(ct);
            hasCloudBackups = cloudObjects.Count > 0;
            if (hasCloudBackups)
            {
                var config = await cloudProviderFactory.GetConfigAsync(ct);
                cloudDetail = $"{cloudObjects.Count} bản sao trên {config.Provider} ({FormatSize(cloudObjects.Sum(o => o.SizeBytes))})";
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Cloud backup check failed");
            cloudDetail = "Cloud không khả dụng: " + ex.Message;
        }
        checks.Add(new BackupComplianceCheck(
            "copy_cloud_offsite",
            "Bản sao offsite — Cloud storage (S3/Azure/GCS)",
            hasCloudBackups,
            cloudDetail
        ));
        if (!hasCloudBackups)
            recommendations.Add("Bật 'Upload to Cloud' trong chiến lược backup để đảm bảo bản sao offsite");

        int copiesCount = (dbInfo.Connected ? 1 : 0) + (hasLocalBackups ? 1 : 0) + (hasCloudBackups ? 1 : 0);

        // ── 2. TWO STORAGE TYPES ─────────────────────────────────
        // Type 1: Local disk (pg_dump files + MinIO tar.gz)
        bool hasLocalDisk = hasLocalBackups;
        checks.Add(new BackupComplianceCheck(
            "storage_local_disk",
            "Loại lưu trữ 1 — Đĩa cục bộ (pg_dump + MinIO archive)",
            hasLocalDisk,
            hasLocalDisk ? "Bản sao lưu trên hệ thống file cục bộ" : "Không có bản sao cục bộ"
        ));

        // Type 2: Object storage / cloud
        bool hasObjectStorage = hasCloudBackups;
        checks.Add(new BackupComplianceCheck(
            "storage_object_cloud",
            "Loại lưu trữ 2 — Object storage / Cloud",
            hasObjectStorage,
            hasObjectStorage ? "Bản sao trên cloud object storage" : "Chưa upload lên cloud storage"
        ));
        if (!hasObjectStorage)
            recommendations.Add("Cấu hình cloud storage (S3/Azure/GCS) cho loại lưu trữ thứ 2");

        int storageTypes = (hasLocalDisk ? 1 : 0) + (hasObjectStorage ? 1 : 0);

        // ── 3. ONE OFFSITE ───────────────────────────────────────
        checks.Add(new BackupComplianceCheck(
            "offsite_cloud",
            "Offsite — Ít nhất 1 bản sao ở vị trí khác (cloud)",
            hasCloudBackups,
            hasCloudBackups ? "Bản sao offsite trên cloud ✓" : "Chưa có bản sao offsite"
        ));
        if (!hasCloudBackups)
            recommendations.Add("Upload bản sao lưu lên cloud để có ít nhất 1 bản offsite");

        int offsiteCopies = hasCloudBackups ? 1 : 0;

        // ── ADDITIONAL FEATURES ──────────────────────────────────
        // WAL archiving status
        var walStatus = await walBackupService.GetWalStatusAsync(ct);
        checks.Add(new BackupComplianceCheck(
            "wal_archiving",
            "WAL Archiving — Point-in-Time Recovery (PITR)",
            walStatus.IsArchivingEnabled,
            walStatus.IsArchivingEnabled
                ? $"WAL archiving đang bật (level: {walStatus.WalLevel}, archived: {walStatus.ArchivedCount})"
                : $"WAL archiving chưa bật (hiện tại: {walStatus.WalLevel}/{walStatus.ArchiveMode})"
        ));
        if (!walStatus.IsArchivingEnabled)
            recommendations.Add("Bật WAL archiving để có khả năng Point-in-Time Recovery");

        // Replication status
        var replStatus = await replicationService.GetStatusAsync(ct);
        var hasReplication = replStatus.IsReplicating;
        checks.Add(new BackupComplianceCheck(
            "replication",
            "Streaming Replication — Sao chép thời gian thực",
            hasReplication,
            hasReplication
                ? $"{replStatus.ConnectedReplicas.Count} standby đang kết nối"
                : "Chưa có standby replication"
        ));
        if (!hasReplication)
            recommendations.Add("Thiết lập PostgreSQL streaming replication cho khả năng chuyển đổi dự phòng (failover)");

        // Base backup
        var baseBackups = walBackupService.ListBaseBackups(BackupsDir);
        checks.Add(new BackupComplianceCheck(
            "base_backup",
            "Base Backup — pg_basebackup cho PITR",
            baseBackups.Count > 0,
            baseBackups.Count > 0
                ? $"{baseBackups.Count} base backup ({FormatSize(baseBackups.Sum(b => b.SizeBytes))})"
                : "Chưa có base backup"
        ));

        // PKI / CA key backups
        var pkiBackups = pkiBackupService.ListBackups();
        checks.Add(new BackupComplianceCheck(
            "pki_backup",
            "PKI / CA Keys — Sao lưu chứng chỉ số & khóa CA",
            pkiBackups.Count > 0,
            pkiBackups.Count > 0
                ? $"{pkiBackups.Count} bản sao PKI ({FormatSize(pkiBackups.Sum(b => b.SizeBytes))})"
                : "Chưa có bản sao lưu PKI/CA keys"
        ));
        if (pkiBackups.Count == 0)
            recommendations.Add("Tạo bản sao lưu PKI/CA keys (để bảo vệ chứng chỉ số và khóa CA)");

        // Backup freshness
        var latestBackup = dbBackups.FirstOrDefault();
        DateTime? latestBackupTime = latestBackup?.CreatedAt;
        bool isFresh = latestBackupTime.HasValue &&
            (DateTime.UtcNow - latestBackupTime.Value).TotalHours < 25;
        checks.Add(new BackupComplianceCheck(
            "backup_freshness",
            "Độ mới — Backup trong 24h gần nhất",
            isFresh,
            latestBackupTime.HasValue
                ? $"Backup gần nhất: {latestBackupTime.Value:yyyy-MM-dd HH:mm} UTC ({(DateTime.UtcNow - latestBackupTime.Value).TotalHours:F1}h trước)"
                : "Chưa có backup nào"
        ));
        if (!isFresh)
            recommendations.Add("Tạo backup ngay hoặc bật chiến lược backup tự động hàng ngày");

        // ── COMPLIANCE SCORE ─────────────────────────────────────
        // 3-2-1: max score = 3 (copies) + 2 (types) + 1 (offsite) = 6
        int ruleScore = Math.Min(copiesCount, 3) + Math.Min(storageTypes, 2) + Math.Min(offsiteCopies, 1);
        bool isCompliant = copiesCount >= 3 && storageTypes >= 2 && offsiteCopies >= 1;

        // Bonus score for additional features (WAL, replication, base backup, freshness, PKI)
        int bonusScore = (walStatus.IsArchivingEnabled ? 1 : 0)
            + (hasReplication ? 1 : 0)
            + (baseBackups.Count > 0 ? 1 : 0)
            + (isFresh ? 1 : 0)
            + (pkiBackups.Count > 0 ? 1 : 0);

        return new BackupComplianceReport(
            IsCompliant: isCompliant,
            RuleScore: ruleScore,
            MaxRuleScore: 6,
            BonusScore: bonusScore,
            MaxBonusScore: 5,
            CopiesCount: copiesCount,
            StorageTypesCount: storageTypes,
            OffsiteCopiesCount: offsiteCopies,
            Checks: checks,
            Recommendations: recommendations,
            Summary: new BackupComplianceSummary(
                TotalDbBackups: dbBackups.Count,
                TotalMinioBackups: minioBackups.Count,
                TotalBaseBackups: baseBackups.Count,
                TotalPkiBackups: pkiBackups.Count,
                LatestBackupTime: latestBackupTime,
                WalArchivingEnabled: walStatus.IsArchivingEnabled,
                ReplicationActive: hasReplication,
                CloudConfigured: hasCloudBackups,
                ReplicaCount: replStatus.ConnectedReplicas.Count
            )
        );
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1073741824) return $"{bytes / 1048576.0:F1} MB";
        return $"{bytes / 1073741824.0:F1} GB";
    }
}

public record BackupComplianceReport(
    bool IsCompliant,
    int RuleScore,
    int MaxRuleScore,
    int BonusScore,
    int MaxBonusScore,
    int CopiesCount,
    int StorageTypesCount,
    int OffsiteCopiesCount,
    List<BackupComplianceCheck> Checks,
    List<string> Recommendations,
    BackupComplianceSummary Summary);

public record BackupComplianceCheck(
    string Id, string Label, bool Passed, string Detail);

public record BackupComplianceSummary(
    int TotalDbBackups,
    int TotalMinioBackups,
    int TotalBaseBackups,
    int TotalPkiBackups,
    DateTime? LatestBackupTime,
    bool WalArchivingEnabled,
    bool ReplicationActive,
    bool CloudConfigured,
    int ReplicaCount);
