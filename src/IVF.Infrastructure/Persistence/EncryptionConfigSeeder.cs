using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Persistence;

public static class EncryptionConfigSeeder
{
    public static async Task SeedAsync(IvfDbContext context)
    {
        if (await context.EncryptionConfigs.AnyAsync())
        {
            Console.WriteLine("[EncryptionConfigSeeder] Configs already exist. Skipping.");
            return;
        }

        Console.WriteLine("[EncryptionConfigSeeder] Seeding default encryption configs...");

        var configs = new List<EncryptionConfig>
        {
            EncryptionConfig.Create("medical_records",
                ["diagnosis", "symptoms", "treatment_plan", "notes", "medications", "allergies"],
                "data", "Hồ sơ bệnh án - dữ liệu PHI", isDefault: true),

            EncryptionConfig.Create("patients",
                ["medical_history", "allergies", "emergency_contact", "insurance_info"],
                "data", "Thông tin bệnh nhân nhạy cảm", isDefault: true),

            EncryptionConfig.Create("prescriptions",
                ["medications", "dosage_instructions", "notes"],
                "data", "Đơn thuốc và hướng dẫn liều", isDefault: true),

            EncryptionConfig.Create("lab_results",
                ["results", "notes", "interpretation"],
                "data", "Kết quả xét nghiệm", isDefault: true),

            EncryptionConfig.Create("user_sessions",
                ["session_token"],
                "session", "Token phiên đăng nhập", isDefault: true),
        };

        await context.EncryptionConfigs.AddRangeAsync(configs);
        await context.SaveChangesAsync();

        Console.WriteLine($"[EncryptionConfigSeeder] Seeded {configs.Count} default configs.");
    }
}
