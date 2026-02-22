namespace IVF.Domain.Enums;

/// <summary>
/// Fine-grained permissions for RBAC
/// </summary>
public enum Permission
{
    // Patient Module
    ViewPatients,
    ManagePatients,

    // Couple & Cycle Module
    ViewCouples,
    ManageCouples,
    ViewCycles,
    ManageCycles,

    // Ultrasound Module
    ViewUltrasounds,
    PerformUltrasound,

    // Embryo Module
    ViewEmbryos,
    ManageEmbryos,

    // Lab Module
    ViewLabResults,
    ManageLabResults,

    // Andrology Module
    ViewAndrology,
    ManageAndrology,

    // Sperm Bank Module
    ViewSpermBank,
    ManageSpermBank,

    // Billing Module
    ViewBilling,
    ManageBilling,
    CreateInvoice,
    ProcessPayment,

    // Queue Module
    ViewQueue,
    ManageQueue,
    CallTicket,

    // Prescription Module
    ViewPrescriptions,
    CreatePrescription,

    // Reports Module
    ViewReports,
    ViewAdminReports,
    ExportData,

    // Scheduling Module
    ViewSchedule,
    ManageSchedule,
    BookAppointment,

    // Forms Module
    ViewForms,
    ManageForms,
    DesignForms,

    // Service Catalog Module
    ViewServices,
    ManageServices,

    // Notification Module
    ViewNotifications,
    ManageNotifications,

    // Digital Signing Module
    ViewDigitalSigning,
    ManageDigitalSigning,
    SignDocuments,

    // Admin Module
    ManageUsers,
    ManageRoles,
    ManageSystem,
    ViewAuditLog
}
