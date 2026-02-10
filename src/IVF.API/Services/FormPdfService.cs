using IVF.Application.Features.Forms.Commands;
using IVF.Application.Features.Forms.Queries;
using IVF.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IVF.API.Services;

public static class FormPdfService
{
    public static byte[] GeneratePdf(FormResponseDto response, FormTemplateDto template)
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
                        // Skip layout-only fields
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
