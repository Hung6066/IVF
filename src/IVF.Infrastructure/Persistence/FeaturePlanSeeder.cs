using IVF.Domain.Entities;
using IVF.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Persistence;

/// <summary>
/// Seeds default feature definitions and plan definitions with their feature mappings.
/// Idempotent — only runs if no feature definitions exist yet.
/// </summary>
public static class FeaturePlanSeeder
{
    public static async Task SeedAsync(IvfDbContext context)
    {
        if (await context.FeatureDefinitions.AnyAsync())
        {
            Console.WriteLine("[FeaturePlanSeeder] Feature definitions already exist. Skipping.");
            return;
        }

        Console.WriteLine("[FeaturePlanSeeder] Seeding features and plans...");

        // ── Feature Definitions ──────────────────────────
        var features = new Dictionary<string, FeatureDefinition>
        {
            ["patient_management"] = FeatureDefinition.Create("patient_management", "Quản lý bệnh nhân", "Hồ sơ bệnh nhân, cặp đôi, lịch sử điều trị", "👥", "core", 10),
            ["appointments"] = FeatureDefinition.Create("appointments", "Lịch hẹn", "Quản lý lịch hẹn khám", "📅", "core", 20),
            ["queue"] = FeatureDefinition.Create("queue", "Hàng đợi", "Quản lý hàng đợi tiếp đón", "🎫", "core", 30),
            ["basic_forms"] = FeatureDefinition.Create("basic_forms", "Biểu mẫu cơ bản", "Biểu mẫu lâm sàng cơ bản", "📝", "core", 40),
            ["billing"] = FeatureDefinition.Create("billing", "Hoá đơn & Thanh toán", "Quản lý hoá đơn, thanh toán", "💰", "core", 50),
            ["consultation"] = FeatureDefinition.Create("consultation", "Tư vấn", "Tư vấn bệnh nhân", "🗣️", "core", 60),
            ["ultrasound"] = FeatureDefinition.Create("ultrasound", "Siêu âm", "Quản lý siêu âm", "🔬", "core", 70),
            ["lab"] = FeatureDefinition.Create("lab", "Phòng Lab", "Xét nghiệm & phòng lab", "🧫", "core", 80),
            ["pharmacy"] = FeatureDefinition.Create("pharmacy", "Nhà thuốc", "Quản lý đơn thuốc", "💊", "core", 90),
            ["advanced_reporting"] = FeatureDefinition.Create("advanced_reporting", "Báo cáo nâng cao", "Dashboard, xuất PDF, biểu đồ thống kê chi tiết", "📈", "advanced", 100),
            ["export_pdf"] = FeatureDefinition.Create("export_pdf", "Export PDF", "Xuất báo cáo PDF chuyên nghiệp", "📄", "advanced", 110),
            ["email_support"] = FeatureDefinition.Create("email_support", "Hỗ trợ email", "Hỗ trợ kỹ thuật qua email", "📧", "advanced", 120),
            ["ai"] = FeatureDefinition.Create("ai", "AI hỗ trợ", "AI hỗ trợ chẩn đoán và phân tích", "🤖", "advanced", 130),
            ["digital_signing"] = FeatureDefinition.Create("digital_signing", "Ký số", "Chữ ký số PKI với EJBCA/SignServer", "🔏", "advanced", 140),
            ["hipaa_gdpr"] = FeatureDefinition.Create("hipaa_gdpr", "HIPAA/GDPR", "Tuân thủ quy chuẩn bảo mật y tế quốc tế", "🛡️", "advanced", 150),
            ["priority_support"] = FeatureDefinition.Create("priority_support", "Hỗ trợ ưu tiên", "Hỗ trợ kỹ thuật ưu tiên nhanh chóng", "⚡", "advanced", 160),
            ["biometrics"] = FeatureDefinition.Create("biometrics", "Sinh trắc học", "Xác minh bệnh nhân bằng vân tay", "👆", "enterprise", 170),
            ["sso_saml"] = FeatureDefinition.Create("sso_saml", "SSO/SAML", "Đăng nhập tập trung SSO/SAML", "🔐", "enterprise", 180),
            ["sla_999"] = FeatureDefinition.Create("sla_999", "SLA 99.9%", "Cam kết uptime 99.9%", "📊", "enterprise", 190),
            ["support_247"] = FeatureDefinition.Create("support_247", "Hỗ trợ 24/7", "Hỗ trợ kỹ thuật 24/7", "🕐", "enterprise", 200),
            ["custom_domain"] = FeatureDefinition.Create("custom_domain", "Custom domain", "Tên miền riêng cho trung tâm", "🌐", "enterprise", 210),
            ["andrology"] = FeatureDefinition.Create("andrology", "Nam khoa", "Module nam khoa chuyên sâu", "🔬", "core", 85),
            ["sperm_bank"] = FeatureDefinition.Create("sperm_bank", "Ngân hàng tinh trùng", "Quản lý ngân hàng tinh trùng", "🏦", "advanced", 95),
            ["injection"] = FeatureDefinition.Create("injection", "Tiêm", "Module quản lý tiêm thuốc", "💉", "core", 75),
        };

        await context.FeatureDefinitions.AddRangeAsync(features.Values);
        await context.SaveChangesAsync();

        // ── Plan Definitions ──────────────────────────
        var trialPlan = PlanDefinition.Create(
            SubscriptionPlan.Trial, "Trial", "Dùng thử 30 ngày miễn phí",
            0, "VND", "30 ngày", 3, 20, 512, 10);

        var starterPlan = PlanDefinition.Create(
            SubscriptionPlan.Starter, "Starter", "Gói khởi đầu cho phòng khám nhỏ",
            5_000_000, "VND", "Tháng", 10, 100, 5_120, 20);

        var professionalPlan = PlanDefinition.Create(
            SubscriptionPlan.Professional, "Professional", "Gói chuyên nghiệp với AI và ký số",
            15_000_000, "VND", "Tháng", 30, 500, 20_480, 30, isFeatured: true);

        var enterprisePlan = PlanDefinition.Create(
            SubscriptionPlan.Enterprise, "Enterprise", "Gói doanh nghiệp toàn diện",
            35_000_000, "VND", "Tháng", 100, 2000, 102_400, 40);

        var customPlan = PlanDefinition.Create(
            SubscriptionPlan.Custom, "Custom", "Gói tuỳ chỉnh theo yêu cầu",
            0, "VND", "Tháng", 999, 99999, 1_048_576, 50);

        var plans = new[] { trialPlan, starterPlan, professionalPlan, enterprisePlan, customPlan };
        await context.PlanDefinitions.AddRangeAsync(plans);
        await context.SaveChangesAsync();

        // ── Plan-Feature Mappings ──────────────────────────

        // Trial: core features only
        var trialFeatures = new[] { "patient_management", "appointments", "queue", "basic_forms" };
        int sort = 0;
        foreach (var code in trialFeatures)
            await context.PlanFeatures.AddAsync(PlanFeature.Create(trialPlan.Id, features[code].Id, ++sort));

        // Starter: Trial + reporting, PDF, email support
        var starterFeatures = new[] { "patient_management", "appointments", "queue", "basic_forms",
            "billing", "consultation", "ultrasound", "lab", "pharmacy", "injection",
            "advanced_reporting", "export_pdf", "email_support" };
        sort = 0;
        foreach (var code in starterFeatures)
            await context.PlanFeatures.AddAsync(PlanFeature.Create(starterPlan.Id, features[code].Id, ++sort));

        // Professional: Starter + AI, signing, HIPAA/GDPR, priority support, andrology, sperm bank
        var professionalFeatures = new[] { "patient_management", "appointments", "queue", "basic_forms",
            "billing", "consultation", "ultrasound", "lab", "pharmacy", "injection",
            "advanced_reporting", "export_pdf", "email_support",
            "ai", "digital_signing", "hipaa_gdpr", "priority_support", "andrology", "sperm_bank" };
        sort = 0;
        foreach (var code in professionalFeatures)
            await context.PlanFeatures.AddAsync(PlanFeature.Create(professionalPlan.Id, features[code].Id, ++sort));

        // Enterprise: Professional + biometrics, SSO, SLA, 24/7, custom domain
        var enterpriseFeatures = new[] { "patient_management", "appointments", "queue", "basic_forms",
            "billing", "consultation", "ultrasound", "lab", "pharmacy", "injection",
            "advanced_reporting", "export_pdf", "email_support",
            "ai", "digital_signing", "hipaa_gdpr", "priority_support", "andrology", "sperm_bank",
            "biometrics", "sso_saml", "sla_999", "support_247", "custom_domain" };
        sort = 0;
        foreach (var code in enterpriseFeatures)
            await context.PlanFeatures.AddAsync(PlanFeature.Create(enterprisePlan.Id, features[code].Id, ++sort));

        // Custom: all features
        sort = 0;
        foreach (var feature in features.Values)
            await context.PlanFeatures.AddAsync(PlanFeature.Create(customPlan.Id, feature.Id, ++sort));

        await context.SaveChangesAsync();

        // ── Seed TenantFeatures for root tenant (all features enabled) ──────────────────
        var rootTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var rootTenant = await context.Tenants.FindAsync(rootTenantId);
        if (rootTenant != null)
        {
            foreach (var feature in features.Values)
                await context.TenantFeatures.AddAsync(TenantFeature.Create(rootTenantId, feature.Id));
            await context.SaveChangesAsync();
        }

        Console.WriteLine($"[FeaturePlanSeeder] Seeded {features.Count} features, {plans.Length} plans, and plan-feature mappings.");
    }
}
