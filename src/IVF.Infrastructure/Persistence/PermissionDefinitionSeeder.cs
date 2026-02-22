using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;

namespace IVF.Infrastructure.Persistence;

/// <summary>
/// Seeds the permission_definitions table from the existing Permission enum values
/// with Vietnamese display names and group metadata.
/// Only runs if the table is empty (idempotent).
/// </summary>
public static class PermissionDefinitionSeeder
{
    public static async Task SeedAsync(IPermissionDefinitionRepository repo, IUnitOfWork uow)
    {
        if (await repo.AnyAsync())
            return;

        var definitions = new List<PermissionDefinition>
        {
            // ── Bệnh nhân (Patient) ──
            P("ViewPatients",    "Xem bệnh nhân",      "patient", "Bệnh nhân",          "👥", 1, 1),
            P("ManagePatients",  "Quản lý bệnh nhân",  "patient", "Bệnh nhân",          "👥", 2, 1),

            // ── Cặp đôi & Chu kỳ (Couple & Cycle) ──
            P("ViewCouples",     "Xem cặp đôi",        "couple",  "Cặp đôi & Chu kỳ",   "💑", 1, 2),
            P("ManageCouples",   "Quản lý cặp đôi",    "couple",  "Cặp đôi & Chu kỳ",   "💑", 2, 2),
            P("ViewCycles",      "Xem chu kỳ",          "couple",  "Cặp đôi & Chu kỳ",   "💑", 3, 2),
            P("ManageCycles",    "Quản lý chu kỳ",      "couple",  "Cặp đôi & Chu kỳ",   "💑", 4, 2),

            // ── Siêu âm (Ultrasound) ──
            P("ViewUltrasounds",  "Xem siêu âm",        "ultrasound", "Siêu âm",          "🔬", 1, 3),
            P("PerformUltrasound","Thực hiện siêu âm",   "ultrasound", "Siêu âm",          "🔬", 2, 3),

            // ── Phôi (Embryo) ──
            P("ViewEmbryos",     "Xem phôi",            "embryo",  "Phôi",               "🧬", 1, 4),
            P("ManageEmbryos",   "Quản lý phôi",        "embryo",  "Phôi",               "🧬", 2, 4),

            // ── Lab ──
            P("ViewLabResults",  "Xem xét nghiệm",     "lab",     "Lab",                "🧫", 1, 5),
            P("ManageLabResults","Quản lý xét nghiệm",  "lab",     "Lab",                "🧫", 2, 5),

            // ── Nam khoa (Andrology) ──
            P("ViewAndrology",   "Xem nam khoa",        "andrology","Nam khoa",           "🔬", 1, 6),
            P("ManageAndrology", "Quản lý nam khoa",    "andrology","Nam khoa",           "🔬", 2, 6),

            // ── Ngân hàng tinh trùng (Sperm Bank) ──
            P("ViewSpermBank",   "Xem NHTT",            "spermbank","Ngân hàng tinh trùng","🏦", 1, 7),
            P("ManageSpermBank", "Quản lý NHTT",        "spermbank","Ngân hàng tinh trùng","🏦", 2, 7),

            // ── Hoá đơn (Billing) ──
            P("ViewBilling",     "Xem hoá đơn",         "billing", "Hoá đơn",            "💰", 1, 8),
            P("ManageBilling",   "Quản lý hoá đơn",     "billing", "Hoá đơn",            "💰", 2, 8),
            P("CreateInvoice",   "Tạo hoá đơn",         "billing", "Hoá đơn",            "💰", 3, 8),
            P("ProcessPayment",  "Xử lý thanh toán",    "billing", "Hoá đơn",            "💰", 4, 8),

            // ── Hàng đợi (Queue) ──
            P("ViewQueue",       "Xem hàng đợi",        "queue",   "Hàng đợi",           "🎫", 1, 9),
            P("ManageQueue",     "Quản lý hàng đợi",    "queue",   "Hàng đợi",           "🎫", 2, 9),
            P("CallTicket",      "Gọi bệnh nhân",       "queue",   "Hàng đợi",           "🎫", 3, 9),

            // ── Đơn thuốc (Prescription) ──
            P("ViewPrescriptions","Xem đơn thuốc",      "prescription","Đơn thuốc",       "💊", 1, 10),
            P("CreatePrescription","Tạo đơn thuốc",     "prescription","Đơn thuốc",       "💊", 2, 10),

            // ── Lịch hẹn (Schedule) ──
            P("ViewSchedule",    "Xem lịch",            "schedule","Lịch hẹn",            "📅", 1, 11),
            P("ManageSchedule",  "Quản lý lịch",        "schedule","Lịch hẹn",            "📅", 2, 11),
            P("BookAppointment", "Đặt lịch hẹn",        "schedule","Lịch hẹn",            "📅", 3, 11),

            // ── Báo cáo (Reports) ──
            P("ViewReports",     "Xem báo cáo",         "report",  "Báo cáo",            "📊", 1, 12),
            P("ViewAdminReports","BC quản trị",          "report",  "Báo cáo",            "📊", 2, 12),
            P("ExportData",      "Xuất dữ liệu",        "report",  "Báo cáo",            "📊", 3, 12),

            // ── Biểu mẫu (Forms) ──
            P("ViewForms",       "Xem biểu mẫu",        "form",    "Biểu mẫu",           "📝", 1, 13),
            P("ManageForms",     "Quản lý biểu mẫu",    "form",    "Biểu mẫu",           "📝", 2, 13),
            P("DesignForms",     "Thiết kế biểu mẫu",   "form",    "Biểu mẫu",           "📝", 3, 13),

            // ── Danh mục DV (Service Catalog) ──
            P("ViewServices",    "Xem danh mục DV",     "service", "Danh mục DV",         "📋", 1, 14),
            P("ManageServices",  "Quản lý danh mục DV", "service", "Danh mục DV",         "📋", 2, 14),

            // ── Thông báo (Notification) ──
            P("ViewNotifications",  "Xem thông báo",     "notification","Thông báo",      "🔔", 1, 15),
            P("ManageNotifications","Quản lý thông báo", "notification","Thông báo",      "🔔", 2, 15),

            // ── Ký số (Digital Signing) ──
            P("ViewDigitalSigning",  "Xem ký số",        "signing", "Ký số",              "🔏", 1, 16),
            P("ManageDigitalSigning","Quản lý ký số",    "signing", "Ký số",              "🔏", 2, 16),
            P("SignDocuments",       "Ký tài liệu",      "signing", "Ký số",              "🔏", 3, 16),

            // ── Quản trị (Admin) ──
            P("ManageUsers",     "Quản lý người dùng",  "admin",   "Quản trị",           "⚙️", 1, 17),
            P("ManageRoles",     "Quản lý vai trò",     "admin",   "Quản trị",           "⚙️", 2, 17),
            P("ManageSystem",    "Quản lý hệ thống",    "admin",   "Quản trị",           "⚙️", 3, 17),
            P("ViewAuditLog",    "Xem nhật ký",          "admin",   "Quản trị",           "⚙️", 4, 17),
        };

        await repo.AddRangeAsync(definitions);
        await uow.SaveChangesAsync();
    }

    private static PermissionDefinition P(
        string code, string displayName,
        string groupCode, string groupDisplayName,
        string groupIcon, int sortOrder, int groupSortOrder)
    {
        return PermissionDefinition.Create(code, displayName, groupCode, groupDisplayName, groupIcon, sortOrder, groupSortOrder);
    }
}
