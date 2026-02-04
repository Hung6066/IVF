namespace IVF.Domain.Enums;

/// <summary>
/// User roles for Role-Based Access Control (RBAC)
/// </summary>
public enum UserRole
{
    Admin,          // Quản trị viên - Full system access
    Director,       // Giám đốc - View all, manage reports
    Doctor,         // Bác sĩ - Manage patients, cycles, ultrasound
    Embryologist,   // Chuyên viên phôi - Manage embryos
    Nurse,          // Điều dưỡng - Assist doctor, manage queue
    LabTech,        // Kỹ thuật viên xét nghiệm - Lab results
    Receptionist,   // Lễ tân - Patient intake, queue
    Cashier,        // Thu ngân - Billing, payments
    Pharmacist      // Dược sĩ - Prescriptions
}
