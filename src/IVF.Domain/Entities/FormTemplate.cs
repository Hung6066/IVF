using IVF.Domain.Common;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace IVF.Domain.Entities;

/// <summary>
/// Template for a dynamic form (similar to Google Forms)
/// </summary>
public class FormTemplate : BaseEntity, ITenantEntity
{
    public Guid CategoryId { get; private set; }
    public Guid TenantId { get; private set; }
    public void SetTenantId(Guid tenantId) { TenantId = tenantId; SetUpdated(); }
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Mã code ngắn gọn dùng trong MinIO path, URL. Ví dụ: "spermanalysis", "initialconsultation".
    /// Tự sinh từ tên nếu không truyền vào: xóa dấu tiếng Việt, giữ a-z0-9, viết liền thường.
    /// Không thay đổi khi đổi tên form → đảm bảo MinIO path ổn định.
    /// </summary>
    public string Code { get; private set; } = string.Empty;

    public string? Description { get; private set; }
    public string Version { get; private set; } = "1.0";
    public bool IsPublished { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

    // Navigation
    public FormCategory Category { get; private set; } = null!;
    public User CreatedByUser { get; private set; } = null!;
    public ICollection<FormField> Fields { get; private set; } = new List<FormField>();
    public ICollection<FormResponse> Responses { get; private set; } = new List<FormResponse>();
    public ICollection<ReportTemplate> ReportTemplates { get; private set; } = new List<ReportTemplate>();

    private FormTemplate() { }

    public static FormTemplate Create(
        Guid categoryId,
        string name,
        Guid? createdByUserId,
        string? description = null,
        string? code = null)
    {
        return new FormTemplate
        {
            Id = Guid.NewGuid(),
            CategoryId = categoryId,
            Name = name,
            Code = string.IsNullOrWhiteSpace(code) ? GenerateCode(name) : NormalizeCode(code),
            Description = description,
            Version = "1.0",
            IsPublished = false,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string? description, string? code = null)
    {
        Name = name;
        Description = description;
        // Code only updated if explicitly provided; keeps MinIO paths stable when name changes
        if (!string.IsNullOrWhiteSpace(code))
            Code = NormalizeCode(code);
        SetUpdated();
    }

    public void UpdateCategory(Guid categoryId)
    {
        CategoryId = categoryId;
        SetUpdated();
    }

    public void Publish()
    {
        IsPublished = true;
        SetUpdated();
    }

    public void Unpublish()
    {
        IsPublished = false;
        SetUpdated();
    }

    public void IncrementVersion()
    {
        var parts = Version.Split('.');
        if (parts.Length == 2 && int.TryParse(parts[1], out var minor))
        {
            Version = $"{parts[0]}.{minor + 1}";
        }
        else
        {
            Version = "1.1";
        }
        SetUpdated();
    }

    /// <summary>
    /// Tự sinh code từ tên form tiếng Việt:
    ///   1. Đổi ký tự đặc biệt tiếng Việt không tự decompose (đ→d)
    ///   2. Unicode FormD normalization → tách dấu ra khỏi chữ
    ///   3. Bỏ NonSpacingMark (dấu thanh, dấu mũ...)
    ///   4. Lowercase, chỉ giữ a-z0-9
    /// Ví dụ: "Phiếu xét nghiệm tinh dịch đồ" → "phieuxetnghiemtinhdichdо"
    ///        "Phiếu khám ban đầu - Vô sinh"   → "phieukhamvandauvosinh"
    /// </summary>
    public static string GenerateCode(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "form";

        // Step 1: explicit replacements for chars that don't decompose via FormD
        var s = name
            .Replace('đ', 'd').Replace('Đ', 'D')
            .Replace('–', '-').Replace('—', '-');

        // Step 2: decompose to base + combining marks
        s = s.Normalize(NormalizationForm.FormD);

        // Step 3: strip combining (NonSpacingMark) characters
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        // Step 4: lowercase, keep only a-z0-9
        s = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        s = Regex.Replace(s, @"[^a-z0-9]", "");

        // Trim to max 50 chars, ensure non-empty
        s = s.Length > 50 ? s[..50] : s;
        return string.IsNullOrEmpty(s) ? "form" : s;
    }

    /// <summary>Chuẩn hóa code do người dùng nhập: lowercase, chỉ a-z0-9, tối đa 50 ký tự</summary>
    public static string NormalizeCode(string code)
    {
        var s = Regex.Replace(code.ToLowerInvariant(), @"[^a-z0-9]", "");
        return s.Length > 50 ? s[..50] : (string.IsNullOrEmpty(s) ? "form" : s);
    }

    public FormField AddField(
        string fieldKey,
        string label,
        Enums.FieldType fieldType,
        int displayOrder,
        bool isRequired = false,
        string? placeholder = null,
        string? optionsJson = null,
        string? validationRulesJson = null,
        string? defaultValue = null,
        string? helpText = null,
        string? conditionalLogicJson = null,
        string? layoutJson = null)
    {
        var field = FormField.Create(
            Id,
            fieldKey,
            label,
            fieldType,
            displayOrder,
            isRequired,
            placeholder,
            optionsJson,
            validationRulesJson,
            defaultValue,
            helpText,
            conditionalLogicJson,
            layoutJson);

        Fields.Add(field);
        SetUpdated();
        return field;
    }
}
