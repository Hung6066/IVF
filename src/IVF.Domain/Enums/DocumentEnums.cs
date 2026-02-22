namespace IVF.Domain.Enums;

/// <summary>
/// Loại tài liệu y tế - tham khảo HL7 FHIR DocumentReference.type
/// </summary>
public enum DocumentType
{
    // Hồ sơ bệnh án
    MedicalRecord,          // Bệnh án điện tử tổng quát
    AdmissionNote,          // Phiếu nhập viện
    DischargeNote,          // Phiếu xuất viện
    ProgressNote,           // Ghi nhận tiến triển

    // Kết quả xét nghiệm & chẩn đoán
    LabResult,              // Kết quả xét nghiệm
    ImagingReport,          // Báo cáo hình ảnh (siêu âm, X-ray)
    PathologyReport,        // Báo cáo giải phẫu bệnh

    // IVF-specific
    SemenAnalysisReport,    // Kết quả phân tích tinh dịch
    EmbryologyReport,       // Báo cáo phôi học
    OocyteReport,           // Báo cáo noãn
    TransferReport,         // Báo cáo chuyển phôi
    CryopreservationReport, // Báo cáo đông lạnh

    // Hành chính & cam kết
    ConsentForm,            // Phiếu cam kết / đồng ý
    IdentityDocument,       // Giấy tờ tùy thân (CMND, CCCD, hộ chiếu)
    InsuranceDocument,      // Giấy tờ bảo hiểm

    // Toa thuốc & điều trị
    Prescription,           // Đơn thuốc
    TreatmentPlan,          // Kế hoạch điều trị

    // PDF ký số
    SignedPdf,              // PDF đã ký số

    // Khác
    Other                   // Tài liệu khác
}

/// <summary>
/// Trạng thái tài liệu
/// </summary>
public enum DocumentStatus
{
    Draft,          // Nháp
    Active,         // Đang sử dụng
    Superseded,     // Đã thay thế (bởi phiên bản mới)
    Archived,       // Đã lưu trữ
    EnteredInError  // Nhập sai
}

/// <summary>
/// Mức độ bảo mật tài liệu - tham khảo HL7 confidentiality codes
/// </summary>
public enum ConfidentialityLevel
{
    Normal,         // Bình thường
    Restricted,     // Hạn chế
    VeryRestricted  // Rất hạn chế (ví dụ: HIV, tâm thần)
}
