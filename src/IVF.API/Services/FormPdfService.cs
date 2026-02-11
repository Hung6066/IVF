using IVF.Application.Features.Forms.Commands;
using IVF.Application.Features.Forms.Queries;
using IVF.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IVF.API.Services;

public static class FormPdfService
{
    // Color palette
    private static readonly string PrimaryBlue = "#1565C0";
    private static readonly string DarkBlue = "#0D47A1";
    // LightBlue reserved for future use
    private static readonly string AccentGreen = "#2E7D32";
    private static readonly string WarnRed = "#C62828";
    private static readonly string BorderGrey = "#BDBDBD";
    private static readonly string HeaderBg = "#F5F5F5";
    private static readonly string SectionBg = "#E8EAF6";

    // WHO 2021 reference ranges for semen analysis
    private static readonly Dictionary<string, (string Unit, string Reference, decimal? LowerLimit, decimal? UpperLimit)> SemenReferenceRanges = new()
    {
        ["volume"] = ("ml", "≥ 1.4", 1.4m, null),
        ["ph"] = ("", "7.2 – 8.0", 7.2m, 8.0m),
        ["liquefaction"] = ("phút", "< 60", null, 60m),
        ["concentration"] = ("triệu/ml", "≥ 16", 16m, null),
        ["total_count"] = ("triệu", "≥ 39", 39m, null),
        ["motility_pr"] = ("%", "≥ 30", 30m, null),
        ["motility_np"] = ("%", "–", null, null),
        ["immotile"] = ("%", "–", null, null),
        ["morphology_normal"] = ("%", "≥ 4", 4m, null),
        ["wbc"] = ("triệu/ml", "< 1.0", null, 1.0m),
        ["vitality"] = ("%", "≥ 54", 54m, null),
    };

    private static readonly HashSet<string> SemenAnalysisKeys =
    [
        "collection_date", "abstinence_days", "collection_method",
        "volume", "ph", "appearance", "liquefaction",
        "concentration", "total_count", "motility_pr", "motility_np",
        "immotile", "morphology_normal", "wbc", "vitality",
        "diagnosis", "notes"
    ];

    public static byte[] GeneratePdf(FormResponseDto response, FormTemplateDto template)
    {
        // Detect semen analysis form by checking field keys
        var isSemenAnalysis = template.Fields != null &&
            template.Fields.Any(f => f.FieldKey == "concentration") &&
            template.Fields.Any(f => f.FieldKey == "motility_pr");

        if (isSemenAnalysis)
            return GenerateSemenAnalysisPdf(response, template);

        return GenerateGenericPdf(response, template);
    }

    #region Semen Analysis PDF

    private static byte[] GenerateSemenAnalysisPdf(FormResponseDto response, FormTemplateDto template)
    {
        var fieldMap = template.Fields!.ToDictionary(f => f.FieldKey, f => f);
        var valueMap = response.FieldValues?.ToDictionary(fv => fv.FormFieldId) ?? new();

        string GetVal(string key)
        {
            if (!fieldMap.TryGetValue(key, out var field)) return "–";
            valueMap.TryGetValue(field.Id, out var fv);
            return GetDisplayValue(field, fv);
        }

        decimal? GetNumVal(string key)
        {
            if (!fieldMap.TryGetValue(key, out var field)) return null;
            valueMap.TryGetValue(field.Id, out var fv);
            return fv?.NumericValue;
        }

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginTop(25);
                page.MarginBottom(20);
                page.MarginHorizontal(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                // === HEADER ===
                page.Header().Column(header =>
                {
                    // Hospital header row
                    header.Item().Row(row =>
                    {
                        // Left: Hospital info
                        row.RelativeItem(3).Column(left =>
                        {
                            left.Item().Text("TRUNG TÂM HỖ TRỢ SINH SẢN")
                                .FontSize(11).Bold().FontColor(DarkBlue);
                            left.Item().Text("PHÒNG XÉT NGHIỆM NAM KHOA")
                                .FontSize(9).FontColor(PrimaryBlue);
                            left.Item().PaddingTop(2).Text("Đ/c: ........................................")
                                .FontSize(8).FontColor(Colors.Grey.Darken1);
                            left.Item().Text("ĐT: ........................................")
                                .FontSize(8).FontColor(Colors.Grey.Darken1);
                        });

                        // Right: QC info
                        row.RelativeItem(2).AlignRight().Column(right =>
                        {
                            right.Item().AlignRight().Text("BỘ Y TẾ")
                                .FontSize(9).Bold().FontColor(Colors.Grey.Darken2);
                            right.Item().AlignRight().Text($"Mã PXN: SA-{response.CreatedAt:yyyyMMdd}")
                                .FontSize(8).FontColor(Colors.Grey.Darken1);
                            right.Item().AlignRight().PaddingTop(2)
                                .Text($"Ngày XN: {(response.SubmittedAt ?? response.CreatedAt):dd/MM/yyyy}")
                                .FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    });

                    // Divider
                    header.Item().PaddingVertical(6).LineHorizontal(2).LineColor(PrimaryBlue);

                    // Title
                    header.Item().AlignCenter().PaddingBottom(2)
                        .Text("PHIẾU KẾT QUẢ XÉT NGHIỆM TINH DỊCH ĐỒ")
                        .FontSize(16).Bold().FontColor(DarkBlue);

                    header.Item().AlignCenter().PaddingBottom(8)
                        .Text("(Theo tiêu chuẩn WHO 2021)")
                        .FontSize(9).Italic().FontColor(Colors.Grey.Darken1);
                });

                // === CONTENT ===
                page.Content().Column(content =>
                {
                    // ── Patient & Sample info block ──
                    content.Item().Border(1).BorderColor(BorderGrey).Column(infoBlock =>
                    {
                        // Patient row
                        infoBlock.Item().Background(HeaderBg).Padding(6).Row(row =>
                        {
                            row.RelativeItem(3).Text(text =>
                            {
                                text.Span("Họ tên: ").FontSize(10).Bold();
                                text.Span(response.PatientName ?? "..............................").FontSize(10);
                            });
                            row.RelativeItem(2).Text(text =>
                            {
                                text.Span("Mã BN: ").FontSize(10).Bold();
                                text.Span(response.PatientId?.ToString()[..8].ToUpper() ?? "............").FontSize(10);
                            });
                        });

                        // Sample info row
                        infoBlock.Item().BorderTop(0.5f).BorderColor(BorderGrey).Padding(6).Row(row =>
                        {
                            row.RelativeItem(2).Text(text =>
                            {
                                text.Span("Ngày lấy mẫu: ").FontSize(9).Bold();
                                text.Span(GetVal("collection_date")).FontSize(9);
                            });
                            row.RelativeItem(1).Text(text =>
                            {
                                text.Span("Ngày kiêng: ").FontSize(9).Bold();
                                text.Span($"{GetVal("abstinence_days")} ngày").FontSize(9);
                            });
                            row.RelativeItem(2).Text(text =>
                            {
                                text.Span("PP lấy mẫu: ").FontSize(9).Bold();
                                text.Span(GetVal("collection_method")).FontSize(9);
                            });
                        });
                    });

                    content.Item().PaddingTop(10);

                    // ── Lab Results Table ──
                    // Table header
                    content.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3.5f);  // Chỉ số
                            cols.RelativeColumn(2f);    // Kết quả
                            cols.RelativeColumn(1.5f);  // Đơn vị
                            cols.RelativeColumn(2f);    // Trị số tham chiếu
                            cols.RelativeColumn(1.5f);  // Đánh giá
                        });

                        // Header row
                        table.Header(h =>
                        {
                            h.Cell().Background(DarkBlue).Padding(6)
                                .Text("CHỈ SỐ").FontSize(9).Bold().FontColor(Colors.White);
                            h.Cell().Background(DarkBlue).Padding(6)
                                .Text("KẾT QUẢ").FontSize(9).Bold().FontColor(Colors.White);
                            h.Cell().Background(DarkBlue).Padding(6)
                                .Text("ĐƠN VỊ").FontSize(9).Bold().FontColor(Colors.White);
                            h.Cell().Background(DarkBlue).Padding(6)
                                .Text("THAM CHIẾU").FontSize(9).Bold().FontColor(Colors.White);
                            h.Cell().Background(DarkBlue).Padding(6)
                                .Text("ĐÁNH GIÁ").FontSize(9).Bold().FontColor(Colors.White);
                        });

                        var rowIndex = 0;

                        // Section: Đại thể
                        SectionRow(table, "I. ĐẠI THỂ (MACROSCOPIC)", ref rowIndex);
                        LabRow(table, "Thể tích", "volume", GetVal("volume"), GetNumVal("volume"), ref rowIndex);
                        LabRow(table, "pH", "ph", GetVal("ph"), GetNumVal("ph"), ref rowIndex);
                        LabRow(table, "Màu sắc", "appearance", GetVal("appearance"), null, ref rowIndex, isText: true);
                        LabRow(table, "Thời gian hóa lỏng", "liquefaction", GetVal("liquefaction"), GetNumVal("liquefaction"), ref rowIndex);

                        // Section: Vi thể
                        SectionRow(table, "II. VI THỂ (MICROSCOPIC)", ref rowIndex);
                        LabRow(table, "Mật độ tinh trùng", "concentration", GetVal("concentration"), GetNumVal("concentration"), ref rowIndex);
                        LabRow(table, "Tổng số tinh trùng", "total_count", GetVal("total_count"), GetNumVal("total_count"), ref rowIndex);
                        LabRow(table, "Di động tiến tới (PR)", "motility_pr", GetVal("motility_pr"), GetNumVal("motility_pr"), ref rowIndex);
                        LabRow(table, "Di động tại chỗ (NP)", "motility_np", GetVal("motility_np"), GetNumVal("motility_np"), ref rowIndex);
                        LabRow(table, "Bất động (IM)", "immotile", GetVal("immotile"), GetNumVal("immotile"), ref rowIndex);
                        LabRow(table, "Hình dạng bình thường", "morphology_normal", GetVal("morphology_normal"), GetNumVal("morphology_normal"), ref rowIndex);

                        // Section: Các chỉ số khác
                        SectionRow(table, "III. CÁC CHỈ SỐ KHÁC", ref rowIndex);
                        LabRow(table, "Bạch cầu (WBC)", "wbc", GetVal("wbc"), GetNumVal("wbc"), ref rowIndex);
                        LabRow(table, "Tỷ lệ sống (Vitality)", "vitality", GetVal("vitality"), GetNumVal("vitality"), ref rowIndex);
                    });

                    content.Item().PaddingTop(10);

                    // ── Diagnosis ──
                    content.Item().Border(1).BorderColor(PrimaryBlue).Column(diagBlock =>
                    {
                        diagBlock.Item().Background(SectionBg).Padding(6)
                            .Text("CHẨN ĐOÁN").FontSize(10).Bold().FontColor(DarkBlue);

                        diagBlock.Item().BorderTop(1).BorderColor(PrimaryBlue).Padding(8)
                            .Text(GetVal("diagnosis"))
                            .FontSize(12).Bold().FontColor(GetDiagnosisColor(GetVal("diagnosis")));
                    });

                    // ── Notes ──
                    var notesVal = GetVal("notes");
                    if (notesVal != "–" && !string.IsNullOrWhiteSpace(notesVal))
                    {
                        content.Item().PaddingTop(8).Column(notesBlock =>
                        {
                            notesBlock.Item().Text("Ghi chú:").FontSize(9).Bold().FontColor(Colors.Grey.Darken2);
                            notesBlock.Item().PaddingTop(2).Text(notesVal)
                                .FontSize(9).Italic().FontColor(Colors.Grey.Darken1);
                        });
                    }

                    content.Item().PaddingTop(16);

                    // ── Signatures ──
                    content.Item().Row(row =>
                    {
                        row.RelativeItem().AlignCenter().Column(left =>
                        {
                            left.Item().AlignCenter().Text("KỸ THUẬT VIÊN")
                                .FontSize(9).Bold().FontColor(Colors.Grey.Darken2);
                            left.Item().AlignCenter().PaddingTop(2)
                                .Text("(Ký và ghi rõ họ tên)")
                                .FontSize(8).Italic().FontColor(Colors.Grey.Medium);
                            left.Item().PaddingTop(40).AlignCenter()
                                .Text("....................................").FontSize(8);
                        });

                        row.RelativeItem().AlignCenter().Column(mid =>
                        {
                            mid.Item().AlignCenter().Text("TRƯỞNG KHOA XN")
                                .FontSize(9).Bold().FontColor(Colors.Grey.Darken2);
                            mid.Item().AlignCenter().PaddingTop(2)
                                .Text("(Ký và ghi rõ họ tên)")
                                .FontSize(8).Italic().FontColor(Colors.Grey.Medium);
                            mid.Item().PaddingTop(40).AlignCenter()
                                .Text("....................................").FontSize(8);
                        });

                        row.RelativeItem().AlignCenter().Column(right =>
                        {
                            right.Item().AlignCenter()
                                .Text($"Ngày {(response.SubmittedAt ?? response.CreatedAt):dd} tháng {(response.SubmittedAt ?? response.CreatedAt):MM} năm {(response.SubmittedAt ?? response.CreatedAt):yyyy}")
                                .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                            right.Item().AlignCenter().Text("BÁC SĨ CHỈ ĐỊNH")
                                .FontSize(9).Bold().FontColor(Colors.Grey.Darken2);
                            right.Item().AlignCenter().PaddingTop(2)
                                .Text("(Ký và ghi rõ họ tên)")
                                .FontSize(8).Italic().FontColor(Colors.Grey.Medium);
                            right.Item().PaddingTop(36).AlignCenter()
                                .Text("....................................").FontSize(8);
                        });
                    });
                });

                // === FOOTER ===
                page.Footer().Column(footer =>
                {
                    footer.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                    footer.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem().Text("Tiêu chuẩn tham chiếu: WHO Laboratory Manual, 6th Edition (2021)")
                            .FontSize(7).Italic().FontColor(Colors.Grey.Medium);
                        row.AutoItem().Text(text =>
                        {
                            text.Span("Trang ").FontSize(7).FontColor(Colors.Grey.Medium);
                            text.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Medium);
                            text.Span("/").FontSize(7).FontColor(Colors.Grey.Medium);
                            text.TotalPages().FontSize(7).FontColor(Colors.Grey.Medium);
                        });
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void SectionRow(TableDescriptor table, string title, ref int rowIndex)
    {
        var bg = SectionBg;
        table.Cell().ColumnSpan(5).Background(bg).PaddingVertical(5).PaddingHorizontal(6)
            .Text(title).FontSize(9).Bold().FontColor(DarkBlue);
        rowIndex++;
    }

    private static void LabRow(TableDescriptor table, string label, string fieldKey, string value,
        decimal? numericValue, ref int rowIndex, bool isText = false)
    {
        var bg = rowIndex % 2 == 0 ? "#FFFFFF" : "#FAFAFA";
        var hasRef = SemenReferenceRanges.TryGetValue(fieldKey, out var refRange);

        var unit = hasRef ? refRange.Unit : "";
        var reference = hasRef ? refRange.Reference : "–";
        var evaluation = "–";
        var evalColor = Colors.Grey.Darken1;

        if (!isText && numericValue.HasValue && hasRef)
        {
            var (isNormal, evalText) = EvaluateResult(numericValue.Value, refRange.LowerLimit, refRange.UpperLimit);
            evaluation = evalText;
            evalColor = isNormal ? AccentGreen : WarnRed;
        }
        else if (isText)
        {
            evaluation = "";
            reference = "–";
        }

        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E0E0E0")
            .PaddingVertical(5).PaddingHorizontal(6)
            .Text(label).FontSize(9);

        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E0E0E0")
            .PaddingVertical(5).PaddingHorizontal(6).AlignCenter()
            .Text(value).FontSize(10).Bold();

        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E0E0E0")
            .PaddingVertical(5).PaddingHorizontal(6).AlignCenter()
            .Text(unit).FontSize(9).FontColor(Colors.Grey.Darken1);

        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E0E0E0")
            .PaddingVertical(5).PaddingHorizontal(6).AlignCenter()
            .Text(reference).FontSize(9).FontColor(Colors.Grey.Darken1);

        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E0E0E0")
            .PaddingVertical(5).PaddingHorizontal(6).AlignCenter()
            .Text(evaluation).FontSize(9).Bold().FontColor(evalColor);

        rowIndex++;
    }

    private static (bool IsNormal, string Text) EvaluateResult(decimal value, decimal? lower, decimal? upper)
    {
        // Only lower bound (≥ lower)
        if (lower.HasValue && !upper.HasValue)
        {
            return value >= lower.Value
                ? (true, "Bình thường")
                : (false, "Thấp ↓");
        }

        // Only upper bound (< upper)
        if (!lower.HasValue && upper.HasValue)
        {
            return value < upper.Value
                ? (true, "Bình thường")
                : (false, "Cao ↑");
        }

        // Both bounds (lower – upper)
        if (lower.HasValue && upper.HasValue)
        {
            if (value < lower.Value) return (false, "Thấp ↓");
            if (value > upper.Value) return (false, "Cao ↑");
            return (true, "Bình thường");
        }

        return (true, "–");
    }

    private static string GetDiagnosisColor(string diagnosis)
    {
        if (string.IsNullOrEmpty(diagnosis) || diagnosis == "–") return Colors.Grey.Darken1;
        var lower = diagnosis.ToLowerInvariant();
        if (lower.Contains("bình thường") || lower.Contains("normal"))
            return AccentGreen;
        return WarnRed;
    }

    #endregion

    #region Generic PDF

    private static byte[] GenerateGenericPdf(FormResponseDto response, FormTemplateDto template)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Text(template.Name)
                        .FontSize(20).Bold().FontColor(Colors.Blue.Darken2);

                    if (!string.IsNullOrEmpty(template.Description))
                    {
                        col.Item().PaddingTop(4).Text(template.Description)
                            .FontSize(10).FontColor(Colors.Grey.Darken1);
                    }

                    col.Item().PaddingTop(8).Row(row =>
                    {
                        row.AutoItem().Text($"Trạng thái: {GetStatusLabel(response.Status)}")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                        row.AutoItem().PaddingLeft(20).Text($"Ngày: {(response.SubmittedAt ?? response.CreatedAt):dd/MM/yyyy HH:mm}")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                        if (!string.IsNullOrEmpty(response.PatientName))
                        {
                            row.AutoItem().PaddingLeft(20).Text($"Bệnh nhân: {response.PatientName}")
                                .FontSize(9).FontColor(Colors.Grey.Darken2);
                        }
                    });

                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(16).Column(col =>
                {
                    if (template.Fields == null || !template.Fields.Any())
                    {
                        col.Item().Text("Không có trường nào").FontColor(Colors.Grey.Medium);
                        return;
                    }

                    var fieldValues = response.FieldValues?
                        .ToDictionary(fv => fv.FormFieldId) ?? new();
                    var fields = template.Fields.OrderBy(f => f.DisplayOrder).ToList();

                    foreach (var field in fields)
                    {
                        if (field.FieldType is FieldType.PageBreak) continue;

                        if (field.FieldType is FieldType.Section)
                        {
                            col.Item().PaddingTop(12).PaddingBottom(4)
                                .Text(field.Label).Bold().FontSize(13)
                                .FontColor(Colors.Blue.Darken1);
                            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                            continue;
                        }

                        if (field.FieldType is FieldType.Label)
                        {
                            col.Item().PaddingTop(4)
                                .Text(field.Label).Italic().FontSize(10)
                                .FontColor(Colors.Grey.Darken1);
                            continue;
                        }

                        fieldValues.TryGetValue(field.Id, out var fv);
                        var displayValue = GetDisplayValue(field, fv);

                        col.Item().PaddingTop(8).Row(row =>
                        {
                            row.RelativeItem(2).Column(label =>
                            {
                                label.Item().Text(field.Label)
                                    .FontSize(10).Bold().FontColor(Colors.Grey.Darken3);
                            });
                            row.RelativeItem(3).Column(val =>
                            {
                                val.Item().Text(displayValue)
                                    .FontSize(11).FontColor(Colors.Black);
                            });
                        });
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Trang ").FontSize(8);
                    text.CurrentPageNumber().FontSize(8);
                    text.Span(" / ").FontSize(8);
                    text.TotalPages().FontSize(8);
                });
            });
        });

        return document.GeneratePdf();
    }

    #endregion

    private static string GetDisplayValue(FormFieldDto field, FormFieldValueDto? fv)
    {
        if (fv == null) return "-";

        switch (field.FieldType)
        {
            case FieldType.Number:
            case FieldType.Decimal:
            case FieldType.Rating:
                return fv.NumericValue?.ToString() ?? "-";

            case FieldType.Date:
                return fv.DateValue?.ToString("dd/MM/yyyy") ?? "-";

            case FieldType.DateTime:
                return fv.DateValue?.ToString("dd/MM/yyyy HH:mm") ?? "-";

            case FieldType.Time:
                return fv.DateValue?.ToString("HH:mm") ?? fv.TextValue ?? "-";

            case FieldType.Checkbox:
                // Checkbox group
                if (fv.Details?.Any() == true)
                    return string.Join(", ", fv.Details.Select(d => d.Label ?? d.Value));
                if (fv.BooleanValue.HasValue)
                    return fv.BooleanValue.Value ? "Có" : "Không";
                return "-";

            case FieldType.MultiSelect:
            case FieldType.Tags:
                if (fv.Details?.Any() == true)
                    return string.Join(", ", fv.Details.Select(d => d.Label ?? d.Value));
                if (!string.IsNullOrEmpty(fv.JsonValue))
                {
                    try
                    {
                        var items = System.Text.Json.JsonSerializer.Deserialize<List<string>>(fv.JsonValue);
                        return items != null ? string.Join(", ", items) : fv.JsonValue;
                    }
                    catch { return fv.TextValue ?? fv.JsonValue; }
                }
                return fv.TextValue ?? "-";

            case FieldType.Dropdown:
            case FieldType.Radio:
                if (fv.Details?.Any() == true)
                    return fv.Details[0].Label ?? fv.Details[0].Value;
                return fv.TextValue ?? "-";

            case FieldType.Address:
                if (!string.IsNullOrEmpty(fv.JsonValue))
                {
                    try
                    {
                        var addr = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(fv.JsonValue);
                        if (addr != null)
                            return string.Join(", ", addr.Values.Where(v => !string.IsNullOrEmpty(v)));
                    }
                    catch { }
                }
                return fv.TextValue ?? "-";

            case FieldType.FileUpload:
                return fv.TextValue ?? "[File]";

            case FieldType.Hidden:
                return fv.TextValue ?? "-";

            case FieldType.Slider:
                if (fv.NumericValue.HasValue)
                {
                    // Try to get unit from options
                    try
                    {
                        if (!string.IsNullOrEmpty(field.OptionsJson))
                        {
                            var cfg = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(field.OptionsJson);
                            if (cfg != null && cfg.TryGetValue("unit", out var unit))
                                return $"{fv.NumericValue.Value} {unit.GetString()}";
                        }
                    }
                    catch { }
                    return fv.NumericValue.Value.ToString();
                }
                return "-";

            case FieldType.Calculated:
                return fv.TextValue ?? fv.NumericValue?.ToString() ?? "-";

            case FieldType.RichText:
                // Strip HTML tags for PDF
                if (!string.IsNullOrEmpty(fv.TextValue))
                    return System.Text.RegularExpressions.Regex.Replace(fv.TextValue, "<[^>]+>", " ").Trim();
                return "-";

            case FieldType.Signature:
                return !string.IsNullOrEmpty(fv.TextValue) ? "[Đã ký]" : "-";

            case FieldType.Lookup:
                if (fv.Details?.Any() == true)
                    return fv.Details[0].Label ?? fv.Details[0].Value;
                return fv.TextValue ?? "-";

            case FieldType.Repeater:
                if (!string.IsNullOrEmpty(fv.JsonValue))
                {
                    try
                    {
                        var rows = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, string>>>(fv.JsonValue);
                        if (rows != null)
                            return $"{rows.Count} dòng dữ liệu";
                    }
                    catch { }
                    return fv.JsonValue;
                }
                return "-";

            default:
                return fv.TextValue ?? fv.NumericValue?.ToString() ?? "-";
        }
    }

    private static string GetStatusLabel(ResponseStatus status) => status switch
    {
        ResponseStatus.Draft => "Bản nháp",
        ResponseStatus.Submitted => "Chờ duyệt",
        ResponseStatus.Reviewed => "Đã xem",
        ResponseStatus.Approved => "Đã duyệt",
        ResponseStatus.Rejected => "Từ chối",
        _ => status.ToString()
    };
}
