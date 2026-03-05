namespace IVF.Application.Common;

/// <summary>
/// Constants for all feature codes used in the tenant feature gating system.
/// These must match the FeatureDefinition.Code values seeded in FeaturePlanSeeder.
/// </summary>
public static class FeatureCodes
{
    public const string PatientManagement = "patient_management";
    public const string Appointments = "appointments";
    public const string Queue = "queue";
    public const string BasicForms = "basic_forms";
    public const string Billing = "billing";
    public const string Consultation = "consultation";
    public const string Ultrasound = "ultrasound";
    public const string Injection = "injection";
    public const string Lab = "lab";
    public const string Andrology = "andrology";
    public const string Pharmacy = "pharmacy";
    public const string SpermBank = "sperm_bank";
    public const string AdvancedReporting = "advanced_reporting";
    public const string ExportPdf = "export_pdf";
    public const string EmailSupport = "email_support";
    public const string Ai = "ai";
    public const string DigitalSigning = "digital_signing";
    public const string HipaaGdpr = "hipaa_gdpr";
    public const string PrioritySupport = "priority_support";
    public const string Biometrics = "biometrics";
    public const string SsoSaml = "sso_saml";
    public const string Sla999 = "sla_999";
    public const string Support247 = "support_247";
    public const string CustomDomain = "custom_domain";
}
