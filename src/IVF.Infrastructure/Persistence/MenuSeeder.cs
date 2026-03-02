using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Persistence;

/// <summary>
/// Seeds default navigation menu items into the database.
/// Idempotent — only runs if no menu items exist yet.
/// </summary>
public static class MenuSeeder
{
    public static async Task SeedAsync(IvfDbContext context)
    {
        if (await context.MenuItems.AnyAsync())
        {
            Console.WriteLine("[MenuSeeder] Menu items already exist. Skipping.");
            return;
        }

        Console.WriteLine("[MenuSeeder] Seeding default menu items...");

        var items = new List<MenuItem>
        {
            // ── Main Menu ────────────────────────────────
            MenuItem.Create(null, null, "📊", "Dashboard",    "/dashboard",    null,                false, 10),
            MenuItem.Create(null, null, "🏥", "Tiếp đón",     "/reception",    "ViewPatients",      false, 20),
            MenuItem.Create(null, null, "👥", "Bệnh nhân",    "/patients",     "ViewPatients",      false, 30),
            MenuItem.Create(null, null, "💑", "Cặp đôi",      "/couples",      "ViewCouples",       false, 40),
            MenuItem.Create(null, null, "🎫", "Hàng đợi",     "/queue/all",    "ViewQueue",         false, 50),
            MenuItem.Create(null, null, "🗣️", "Tư vấn",      "/consultation", "ViewCycles",        false, 60),
            MenuItem.Create(null, null, "🔬", "Siêu âm",      "/ultrasound",   "ViewUltrasounds",   false, 70),
            MenuItem.Create(null, null, "🧫", "Phòng Lab",    "/lab",          "ViewLabResults",    false, 80),
            MenuItem.Create(null, null, "🔬", "Nam khoa",      "/andrology",    "ViewAndrology",     false, 90),
            MenuItem.Create(null, null, "💉", "Tiêm",         "/injection",    "ViewCycles",        false, 100),
            MenuItem.Create(null, null, "🏦", "NHTT",         "/sperm-bank",   "ViewSpermBank",     false, 110),
            MenuItem.Create(null, null, "💊", "Nhà thuốc",    "/pharmacy",     "ViewPrescriptions", false, 120),
            MenuItem.Create(null, null, "💰", "Hoá đơn",      "/billing",      "ViewBilling",       false, 130),
            MenuItem.Create(null, null, "📅", "Lịch hẹn",     "/appointments", "ViewSchedule",      false, 140),
            MenuItem.Create(null, null, "📈", "Báo cáo",      "/reports",      "ViewReports",       false, 150),

            // ── Admin Section ────────────────────────────
            MenuItem.Create("admin", "Quản trị", "👥", "Người dùng",   "/admin/users",           "ManageUsers",  false, 10),
            MenuItem.Create("admin", null,        "🔐", "Phân quyền",  "/admin/permissions",     null,           true,  20),
            MenuItem.Create("admin", null,        "📋", "Danh mục DV", "/admin/services",        null,           true,  30),
            MenuItem.Create("admin", null,        "📝", "Biểu mẫu",   "/forms",                 null,           true,  40),
            MenuItem.Create("admin", null,        "📁", "Danh mục BM", "/forms/categories",      null,           true,  50),
            MenuItem.Create("admin", null,        "📝", "Nhật ký",     "/admin/audit-logs",      "ViewAuditLog", false, 60),
            MenuItem.Create("admin", null,        "🔔", "Thông báo",   "/admin/notifications",   null,           true,  70),
            MenuItem.Create("admin", null,        "🔏", "Ký số",       "/admin/digital-signing",  null,           true,  80),
            MenuItem.Create("admin", null,        "⚙️", "Cấu hình menu", "/admin/menu",              null,           true,  90),
            MenuItem.Create("admin", null,        "🛡️", "Cấu hình quyền", "/admin/permission-config", null,         true, 100),
            MenuItem.Create("admin", null,        "🔐", "Bảo mật nâng cao", "/admin/security", null,        true, 105),
        };

        await context.MenuItems.AddRangeAsync(items);
        await context.SaveChangesAsync();

        Console.WriteLine($"[MenuSeeder] Seeded {items.Count} menu items.");
    }
}
