using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Persistence;

/// <summary>
/// Seeds default backup strategies implementing the 3-2-1 backup rule.
/// Idempotent — only runs if no strategies exist yet.
/// </summary>
public static class BackupStrategySeeder
{
    public static async Task SeedAsync(IvfDbContext context)
    {
        if (await context.DataBackupStrategies.AnyAsync())
        {
            Console.WriteLine("[BackupStrategySeeder] Strategies already exist. Skipping.");
            return;
        }

        Console.WriteLine("[BackupStrategySeeder] Seeding default backup strategies (3-2-1 rule)...");

        var strategies = new List<DataBackupStrategy>
        {
            // Daily: Full Backup at 2 AM
            DataBackupStrategy.Create(
                name: "Sao lưu đầy đủ hàng đêm",
                description: "Full backup cơ sở dữ liệu + MinIO mỗi đêm lúc 2:00 AM UTC. Giữ lại 14 ngày, tối đa 14 bản. Bản lâu dài qua chiến lược offsite hàng tuần.",
                includeDatabase: true,
                includeMinio: true,
                cronExpression: "0 2 * * *",
                uploadToCloud: false,
                retentionDays: 14,
                maxBackupCount: 14),

            // Every 6 hours: Database-only backup
            DataBackupStrategy.Create(
                name: "Sao lưu DB mỗi 6 giờ",
                description: "Backup cơ sở dữ liệu mỗi 6 giờ (0h, 6h, 12h, 18h UTC). Giữ lại 7 ngày.",
                includeDatabase: true,
                includeMinio: false,
                cronExpression: "0 */6 * * *",
                uploadToCloud: false,
                retentionDays: 7,
                maxBackupCount: 28),

            // Weekly: Offsite backup to cloud — Sunday 3 AM
            DataBackupStrategy.Create(
                name: "Sao lưu offsite hàng tuần",
                description: "Full backup + upload lên cloud mỗi Chủ Nhật lúc 3:00 AM UTC. Quy tắc 3-2-1: bản sao offsite.",
                includeDatabase: true,
                includeMinio: true,
                cronExpression: "0 3 * * 0",
                uploadToCloud: true,
                retentionDays: 90,
                maxBackupCount: 12),
        };

        await context.DataBackupStrategies.AddRangeAsync(strategies);
        await context.SaveChangesAsync();

        Console.WriteLine($"[BackupStrategySeeder] Seeded {strategies.Count} default backup strategies.");
    }
}
