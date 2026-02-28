using System.Text.Json;
using System.Text.RegularExpressions;
using IVF.Application.Features.Forms.Commands;
using IVF.Application.Features.Forms.Queries;
using IVF.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IVF.API.Services;

/// <summary>
/// Generates DevExpress-style PDF reports from aggregate form data.
/// Supports Table, BarChart, PieChart, Summary report types.
/// Reads configurationJson for page/column/header/footer customization.
/// </summary>
public static class ReportPdfService
{
    private static readonly string PrimaryColor = "#667eea";
    private static readonly string SuccessColor = "#10b981";
    private static readonly string HeaderBg = "#f8fafc";
    private static readonly string BorderColor = "#e2e8f0";

    /// <summary>
    /// Context for embedding handwritten signature images during PDF generation.
    /// When sign=true, signature images from UserSignature entities are rendered
    /// at positions defined by signatureZone controls in the report designer.
    /// </summary>
    public record SignatureContext(
        string? SignerName,
        byte[]? SignatureImageBytes,
        string? SignedDate = null);

    public static byte[] GenerateReportPdf(
        ReportDataDto reportData,
        DateTime? filterFrom = null,
        DateTime? filterTo = null,
        SignatureContext? signatureContext = null,
        Dictionary<string, SignatureContext>? roleSignatures = null)
    {
        var template = reportData.Template;
        var data = reportData.Data;
        var summary = reportData.Summary;

        // Parse configuration
        ReportConfigDto? config = null;
        ReportDesignDto? design = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(template.ConfigurationJson)
                && template.ConfigurationJson != "{}")
            {
                // Detect band-based design by checking for "bands" property
                using var jsonDoc = JsonDocument.Parse(template.ConfigurationJson);
                if (jsonDoc.RootElement.TryGetProperty("bands", out _))
                {
                    design = JsonSerializer.Deserialize<ReportDesignDto>(template.ConfigurationJson);
                }
                else
                {
                    config = JsonSerializer.Deserialize<ReportConfigDto>(template.ConfigurationJson);
                }
            }
        }
        catch { /* use defaults */ }

        // If band-based design is present, use the band renderer
        if (design?.Bands is { Count: > 0 })
        {
            return GenerateBandBasedPdf(template, data, summary, design, filterFrom, filterTo, signatureContext, roleSignatures);
        }

        // Page settings from config
        var pageConfig = config?.Page;
        var headerConfig = config?.Header;
        var footerConfig = config?.Footer;

        // Determine visible column configs
        var visibleColumnConfigs = config?.Columns?
            .Where(c => c.Visible)
            .ToList();

        // Column label map (GroupBy to handle duplicate keys safely)
        var columnLabels = visibleColumnConfigs?
            .GroupBy(c => c.FieldKey)
            .ToDictionary(g => g.Key, g => g.First().Label) ?? [];

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                // ===== PAGE SIZE & ORIENTATION from config =====
                var pageSize = ResolvePageSize(pageConfig?.Size ?? "A4");
                page.Size(string.Equals(pageConfig?.Orientation, "portrait", StringComparison.OrdinalIgnoreCase)
                    ? pageSize
                    : pageSize.Landscape());

                var m = pageConfig?.Margins;
                page.MarginTop(m?.Top ?? 30);
                page.MarginRight(m?.Right ?? 30);
                page.MarginBottom(m?.Bottom ?? 30);
                page.MarginLeft(m?.Left ?? 30);
                page.DefaultTextStyle(x => x.FontSize(9));

                // ===== HEADER from config =====
                page.Header().Column(col =>
                {
                    // Title bar
                    col.Item().Background(Color.FromHex(PrimaryColor)).Padding(16).Row(row =>
                    {
                        row.RelativeItem().Column(titleCol =>
                        {
                            var title = !string.IsNullOrWhiteSpace(headerConfig?.Title)
                                ? headerConfig.Title
                                : template.Name;
                            titleCol.Item().Text(title)
                                .FontSize(18).Bold().FontColor(Colors.White);

                            var subtitle = !string.IsNullOrWhiteSpace(headerConfig?.Subtitle)
                                ? headerConfig.Subtitle
                                : template.Description;
                            if (!string.IsNullOrEmpty(subtitle))
                            {
                                titleCol.Item().PaddingTop(4).Text(subtitle)
                                    .FontSize(9).FontColor(Colors.White).Light();
                            }
                        });

                        row.AutoItem().AlignRight().AlignMiddle().Column(infoCol =>
                        {
                            infoCol.Item().Text($"Loại: {GetReportTypeName(template.ReportType)}")
                                .FontSize(8).FontColor(Colors.White);
                            if (headerConfig?.ShowDate != false)
                            {
                                infoCol.Item().Text($"Xuất: {DateTime.Now:dd/MM/yyyy HH:mm}")
                                    .FontSize(8).FontColor(Colors.White).Light();
                            }
                        });
                    });

                    // Filter info bar
                    col.Item().Background(Color.FromHex(HeaderBg))
                        .BorderBottom(1).BorderColor(Color.FromHex(BorderColor))
                        .Padding(10).Row(row =>
                        {
                            row.AutoItem().Text("📊 Form: ").FontSize(8).Bold();
                            row.AutoItem().Text(template.FormTemplateName)
                                .FontSize(8).FontColor(Colors.Blue.Darken2);

                            if (filterFrom.HasValue || filterTo.HasValue)
                            {
                                row.AutoItem().PaddingLeft(20).Text("📅 Khoảng thời gian: ").FontSize(8).Bold();
                                var fromStr = filterFrom?.ToString("dd/MM/yyyy") ?? "...";
                                var toStr = filterTo?.ToString("dd/MM/yyyy") ?? "...";
                                row.AutoItem().Text($"{fromStr} → {toStr}").FontSize(8);
                            }

                            if (summary != null)
                            {
                                row.AutoItem().PaddingLeft(20).Text("📝 Tổng phản hồi: ").FontSize(8).Bold();
                                row.AutoItem().Text($"{summary.TotalResponses}")
                                    .FontSize(8).FontColor(Color.FromHex(SuccessColor)).Bold();
                            }
                        });
                });

                // ===== CONTENT =====
                page.Content().PaddingTop(12).Column(col =>
                {
                    // Summary cards section
                    if (summary != null)
                    {
                        RenderSummaryCards(col, summary);
                        col.Item().PaddingTop(12);
                    }

                    // Main report content based on type
                    switch (template.ReportType)
                    {
                        case ReportType.Table:
                            if (!string.IsNullOrEmpty(config?.GroupBy))
                                RenderGroupedDataTable(col, data, config, columnLabels);
                            else
                                RenderDataTable(col, data, config, columnLabels);
                            break;
                        case ReportType.BarChart:
                            RenderBarChart(col, summary);
                            break;
                        case ReportType.PieChart:
                            RenderPieChart(col, summary);
                            break;
                        case ReportType.Summary:
                            RenderSummaryGrid(col, summary);
                            break;
                        default:
                            RenderDataTable(col, data, config, columnLabels);
                            break;
                    }
                });

                // ===== FOOTER from config =====
                page.Footer().Row(row =>
                {
                    var footerText = !string.IsNullOrWhiteSpace(footerConfig?.Text)
                        ? footerConfig.Text
                        : $"IVF Report System — {template.Name}";

                    row.RelativeItem().AlignLeft()
                        .Text(footerText)
                        .FontSize(7).FontColor(Colors.Grey.Medium);

                    if (footerConfig?.ShowPageNumber != false)
                    {
                        row.RelativeItem().AlignCenter().Text(text =>
                        {
                            text.Span("Trang ").FontSize(7).FontColor(Colors.Grey.Medium);
                            text.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Medium);
                            text.Span(" / ").FontSize(7).FontColor(Colors.Grey.Medium);
                            text.TotalPages().FontSize(7).FontColor(Colors.Grey.Medium);
                        });
                    }

                    row.RelativeItem().AlignRight()
                        .Text($"Xuất lúc: {DateTime.Now:dd/MM/yyyy HH:mm:ss}")
                        .FontSize(7).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return document.GeneratePdf();
    }

    #region Summary Cards

    private static void RenderSummaryCards(ColumnDescriptor col, ReportSummaryDto summary)
    {
        // Compute averages
        var averages = summary.FieldValueAverages
            .Where(kvp => kvp.Value.HasValue)
            .Take(4)
            .ToList();

        col.Item().Row(row =>
        {
            // Total responses card
            RenderCard(row.RelativeItem(), "📊", "Tổng phản hồi",
                summary.TotalResponses.ToString(), SuccessColor);

            // Average cards
            foreach (var avg in averages)
            {
                RenderCard(row.RelativeItem(), "📈", $"TB {avg.Key}",
                    avg.Value!.Value.ToString("N2"), PrimaryColor);
            }
        });
    }

    private static void RenderCard(IContainer container, string icon, string label, string value, string color)
    {
        container.Padding(4).Border(1).BorderColor(Color.FromHex(BorderColor))
            .Background(Colors.White).Padding(10).Column(col =>
            {
                col.Item().Row(r =>
                {
                    r.AutoItem().Text(icon).FontSize(16);
                    r.AutoItem().PaddingLeft(8).Column(c =>
                    {
                        c.Item().Text(value).FontSize(16).Bold().FontColor(Color.FromHex(color));
                        c.Item().Text(label).FontSize(7).FontColor(Colors.Grey.Darken1);
                    });
                });
            });
    }

    #endregion

    #region Data Table

    private static void RenderDataTable(
        ColumnDescriptor col,
        List<Dictionary<string, object?>> data,
        ReportConfigDto? config,
        Dictionary<string, string>? configLabels = null)
    {
        if (data == null || data.Count == 0)
        {
            col.Item().Padding(20).AlignCenter()
                .Text("Không có dữ liệu").FontColor(Colors.Grey.Medium).Italic();
            return;
        }

        var columns = data[0].Keys.Where(k => k != "responseId").ToList();

        // Column display labels — config labels override defaults
        var displayLabels = new Dictionary<string, string>
        {
            ["patientName"] = "Bệnh nhân",
            ["submittedAt"] = "Ngày nộp",
            ["status"] = "Trạng thái"
        };

        if (configLabels != null)
            foreach (var kvp in configLabels)
                displayLabels[kvp.Key] = kvp.Value;

        var conditionalFormats = config?.ConditionalFormats ?? [];

        col.Item().Table(table =>
        {
            table.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(30);
                foreach (var column in columns)
                {
                    if (column is "patientName") cd.RelativeColumn(2);
                    else if (column is "submittedAt") cd.RelativeColumn(1.5f);
                    else if (column is "status") cd.RelativeColumn(1);
                    else cd.RelativeColumn(1.5f);
                }
            });

            table.Header(header =>
            {
                header.Cell().Background(Color.FromHex(PrimaryColor)).Padding(6)
                    .Text("#").FontSize(8).Bold().FontColor(Colors.White).AlignCenter();

                foreach (var column in columns)
                {
                    var label = displayLabels.GetValueOrDefault(column, column);
                    header.Cell().Background(Color.FromHex(PrimaryColor)).Padding(6)
                        .Text(label).FontSize(8).Bold().FontColor(Colors.White);
                }
            });

            for (int i = 0; i < data.Count; i++)
            {
                var row = data[i];
                var bgColor = i % 2 == 0 ? Colors.White : Color.FromHex("#f8fafc");

                // Check row-level conditional formats
                var rowFormat = EvaluateConditionalFormats(row, conditionalFormats, "row");

                table.Cell().Background(rowFormat?.BackgroundColor != null ? Color.FromHex(rowFormat.BackgroundColor) : bgColor).Padding(5)
                    .Text($"{i + 1}").FontSize(8).FontColor(Colors.Grey.Darken1).AlignCenter();

                foreach (var column in columns)
                {
                    row.TryGetValue(column, out var value);
                    var displayValue = FormatCellValue(column, value);

                    // Check cell-level conditional formats
                    var cellFormat = EvaluateConditionalFormats(row, conditionalFormats, "cell", column);
                    var effectiveFormat = cellFormat ?? rowFormat;
                    var cellBg = effectiveFormat?.BackgroundColor != null ? Color.FromHex(effectiveFormat.BackgroundColor) : bgColor;
                    var textColor = effectiveFormat?.TextColor;
                    var isBold = effectiveFormat?.FontWeight == "bold";

                    var cell = table.Cell().Background(cellBg).Padding(5);

                    if (column == "status")
                    {
                        RenderStatusBadge(cell, displayValue);
                    }
                    else
                    {
                        var txt = cell.Text(displayValue).FontSize(8);
                        if (textColor != null) txt.FontColor(Color.FromHex(textColor));
                        if (isBold) txt.Bold();
                    }
                }
            }

            // Footer aggregations
            if (config?.ShowFooterAggregations == true && config.Columns is { Count: > 0 })
            {
                var aggColumns = config.Columns.Where(c => c.Visible && !string.IsNullOrEmpty(c.Aggregation) && c.Aggregation != "none").ToList();
                if (aggColumns.Count > 0)
                {
                    table.Cell().Background(Color.FromHex("#eff6ff")).Padding(5)
                        .Text("Σ").FontSize(8).Bold().FontColor(Color.FromHex(PrimaryColor)).AlignCenter();

                    foreach (var column in columns)
                    {
                        var aggCol = aggColumns.FirstOrDefault(c => c.FieldKey == column);
                        var cellContainer = table.Cell().Background(Color.FromHex("#eff6ff")).Padding(5);
                        if (aggCol != null)
                        {
                            var aggValue = ComputeAggregation(data, aggCol.FieldKey, aggCol.Aggregation!);
                            cellContainer.Text(aggValue).FontSize(8).Bold().FontColor(Color.FromHex(PrimaryColor));
                        }
                        else
                        {
                            cellContainer.Text("");
                        }
                    }
                }
            }
        });

        col.Item().PaddingTop(8).AlignRight()
            .Text($"Hiển thị {data.Count} bản ghi")
            .FontSize(7).FontColor(Colors.Grey.Medium).Italic();
    }

    /// <summary>
    /// Renders a grouped data table with group headers and group footer aggregations.
    /// </summary>
    private static void RenderGroupedDataTable(
        ColumnDescriptor col,
        List<Dictionary<string, object?>> data,
        ReportConfigDto config,
        Dictionary<string, string>? configLabels = null)
    {
        if (data == null || data.Count == 0)
        {
            col.Item().Padding(20).AlignCenter()
                .Text("Không có dữ liệu").FontColor(Colors.Grey.Medium).Italic();
            return;
        }

        var groupBy = config.GroupBy!;
        var groupSummary = config.GroupSummary;
        var columns = data[0].Keys.Where(k => k != "responseId").ToList();

        var displayLabels = new Dictionary<string, string>
        {
            ["patientName"] = "Bệnh nhân",
            ["submittedAt"] = "Ngày nộp",
            ["status"] = "Trạng thái"
        };
        if (configLabels != null)
            foreach (var kvp in configLabels)
                displayLabels[kvp.Key] = kvp.Value;

        var conditionalFormats = config.ConditionalFormats ?? [];

        // Group data
        var groups = data
            .GroupBy(r => r.GetValueOrDefault(groupBy)?.ToString() ?? "-")
            .ToList();

        var groupLabel = displayLabels.GetValueOrDefault(groupBy, groupBy);

        foreach (var group in groups)
        {
            // Group header
            if (groupSummary?.ShowGroupHeaders != false)
            {
                col.Item().PaddingTop(12).Background(Color.FromHex("#eff6ff"))
                    .BorderBottom(2).BorderColor(Color.FromHex(PrimaryColor))
                    .Padding(8).Row(row =>
                    {
                        row.AutoItem().Text($"📂 {groupLabel}: ").FontSize(9).Bold().FontColor(Color.FromHex(PrimaryColor));
                        row.AutoItem().Text(group.Key).FontSize(9).Bold();
                        row.AutoItem().PaddingLeft(12).Text($"({group.Count()} dòng)").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
            }

            // Group table
            var groupData = group.ToList();
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cd =>
                {
                    cd.ConstantColumn(30);
                    foreach (var column in columns)
                    {
                        if (column is "patientName") cd.RelativeColumn(2);
                        else if (column is "submittedAt") cd.RelativeColumn(1.5f);
                        else if (column is "status") cd.RelativeColumn(1);
                        else cd.RelativeColumn(1.5f);
                    }
                });

                table.Header(header =>
                {
                    header.Cell().Background(Color.FromHex(PrimaryColor)).Padding(6)
                        .Text("#").FontSize(8).Bold().FontColor(Colors.White).AlignCenter();
                    foreach (var column in columns)
                    {
                        var label = displayLabels.GetValueOrDefault(column, column);
                        header.Cell().Background(Color.FromHex(PrimaryColor)).Padding(6)
                            .Text(label).FontSize(8).Bold().FontColor(Colors.White);
                    }
                });

                for (int i = 0; i < groupData.Count; i++)
                {
                    var dataRow = groupData[i];
                    var bgColor = i % 2 == 0 ? Colors.White : Color.FromHex("#f8fafc");
                    var rowFormat = EvaluateConditionalFormats(dataRow, conditionalFormats, "row");

                    table.Cell().Background(rowFormat?.BackgroundColor != null ? Color.FromHex(rowFormat.BackgroundColor) : bgColor).Padding(5)
                        .Text($"{i + 1}").FontSize(8).FontColor(Colors.Grey.Darken1).AlignCenter();

                    foreach (var column in columns)
                    {
                        dataRow.TryGetValue(column, out var value);
                        var displayValue = FormatCellValue(column, value);
                        var cellFormat = EvaluateConditionalFormats(dataRow, conditionalFormats, "cell", column);
                        var effectiveFormat = cellFormat ?? rowFormat;
                        var cellBg = effectiveFormat?.BackgroundColor != null ? Color.FromHex(effectiveFormat.BackgroundColor) : bgColor;
                        var textColor = effectiveFormat?.TextColor;
                        var isBold = effectiveFormat?.FontWeight == "bold";

                        var cell = table.Cell().Background(cellBg).Padding(5);
                        if (column == "status") RenderStatusBadge(cell, displayValue);
                        else
                        {
                            var txt = cell.Text(displayValue).FontSize(8);
                            if (textColor != null) txt.FontColor(Color.FromHex(textColor));
                            if (isBold) txt.Bold();
                        }
                    }
                }
            });

            // Group footer with aggregations
            if (groupSummary?.ShowGroupFooters == true && groupSummary.Aggregations is { Count: > 0 })
            {
                col.Item().Background(Color.FromHex("#f8fafc"))
                    .BorderTop(1).BorderColor(Color.FromHex(BorderColor))
                    .Padding(6).Row(row =>
                    {
                        foreach (var agg in groupSummary.Aggregations)
                        {
                            var aggLabel = !string.IsNullOrEmpty(agg.Label) ? agg.Label : $"{agg.FieldKey} {agg.Type}";
                            var aggValue = ComputeAggregation(groupData, agg.FieldKey, agg.Type);
                            row.AutoItem().PaddingRight(16).Text(text =>
                            {
                                text.Span($"{aggLabel}: ").FontSize(8).FontColor(Colors.Grey.Darken2);
                                text.Span(aggValue).FontSize(8).Bold().FontColor(Color.FromHex(PrimaryColor));
                            });
                        }
                    });
            }
        }

        col.Item().PaddingTop(8).AlignRight()
            .Text($"Hiển thị {data.Count} bản ghi trong {groups.Count} nhóm")
            .FontSize(7).FontColor(Colors.Grey.Medium).Italic();
    }

    /// <summary>
    /// Evaluates conditional format rules against a data row.
    /// Returns the first matching rule's style, or null if no rules match.
    /// </summary>
    private static ConditionalFormatStyleDto? EvaluateConditionalFormats(
        Dictionary<string, object?> row,
        List<ConditionalFormatConfigDto> rules,
        string applyTo,
        string? fieldKey = null)
    {
        foreach (var rule in rules)
        {
            if (!string.Equals(rule.ApplyTo, applyTo, StringComparison.OrdinalIgnoreCase))
                continue;
            if (applyTo == "cell" && !string.Equals(rule.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase))
                continue;

            var cellVal = row.GetValueOrDefault(rule.FieldKey)?.ToString() ?? "";
            var ruleVal = rule.Value ?? "";

            var match = rule.Operator switch
            {
                "eq" => string.Equals(cellVal, ruleVal, StringComparison.OrdinalIgnoreCase),
                "neq" => !string.Equals(cellVal, ruleVal, StringComparison.OrdinalIgnoreCase),
                "contains" => cellVal.Contains(ruleVal, StringComparison.OrdinalIgnoreCase),
                "gt" => decimal.TryParse(cellVal, out var gv) && decimal.TryParse(ruleVal, out var gf) && gv > gf,
                "lt" => decimal.TryParse(cellVal, out var lv) && decimal.TryParse(ruleVal, out var lf) && lv < lf,
                "gte" => decimal.TryParse(cellVal, out var gev) && decimal.TryParse(ruleVal, out var gef) && gev >= gef,
                "lte" => decimal.TryParse(cellVal, out var lev) && decimal.TryParse(ruleVal, out var lef) && lev <= lef,
                "empty" => string.IsNullOrWhiteSpace(cellVal) || cellVal == "-",
                "notEmpty" => !string.IsNullOrWhiteSpace(cellVal) && cellVal != "-",
                _ => false
            };

            if (match) return rule.Style;
        }

        return null;
    }

    /// <summary>
    /// Computes an aggregation (count, sum, avg, min, max) over a set of data rows for a given field.
    /// </summary>
    private static string ComputeAggregation(List<Dictionary<string, object?>> rows, string fieldKey, string aggType)
    {
        var values = rows.Select(r => r.GetValueOrDefault(fieldKey)?.ToString()).Where(v => v != null).ToList();

        return aggType switch
        {
            "count" => values.Count.ToString(),
            "sum" => values.Select(v => decimal.TryParse(v, out var d) ? d : 0).Sum().ToString("N2"),
            "avg" => values.Count > 0
                ? (values.Select(v => decimal.TryParse(v, out var d) ? d : 0).Sum() / values.Count).ToString("N2")
                : "-",
            "min" => values.Select(v => decimal.TryParse(v, out var d) ? d : decimal.MaxValue)
                .Where(d => d != decimal.MaxValue).DefaultIfEmpty(0).Min().ToString("N2"),
            "max" => values.Select(v => decimal.TryParse(v, out var d) ? d : decimal.MinValue)
                .Where(d => d != decimal.MinValue).DefaultIfEmpty(0).Max().ToString("N2"),
            _ => "-"
        };
    }

    private static void RenderStatusBadge(IContainer container, string status)
    {
        var (bgHex, textHex) = status switch
        {
            "Đã duyệt" or "Approved" => ("#dcfce7", "#166534"),
            "Chờ duyệt" or "Submitted" => ("#fef3c7", "#92400e"),
            "Bản nháp" or "Draft" => ("#f1f5f9", "#475569"),
            "Từ chối" or "Rejected" => ("#fee2e2", "#991b1b"),
            _ => ("#f1f5f9", "#475569")
        };

        container.Background(Color.FromHex(bgHex)).Padding(3)
            .Text(status).FontSize(7).Bold().FontColor(Color.FromHex(textHex)).AlignCenter();
    }

    #endregion

    #region Bar Chart

    private static void RenderBarChart(ColumnDescriptor col, ReportSummaryDto? summary)
    {
        if (summary == null) return;

        var items = summary.FieldValueCounts
            .Select(kvp => new { Label = kvp.Key.Contains(':') ? kvp.Key.Split(':')[1] : kvp.Key, Count = kvp.Value })
            .OrderByDescending(x => x.Count)
            .Take(15)
            .ToList();

        if (items.Count == 0)
        {
            col.Item().Padding(20).AlignCenter()
                .Text("Không có dữ liệu biểu đồ").FontColor(Colors.Grey.Medium);
            return;
        }

        var maxValue = items.Max(x => x.Count);

        col.Item().Text("Phân bố giá trị").FontSize(12).Bold().FontColor(Color.FromHex(PrimaryColor));
        col.Item().PaddingTop(8);

        foreach (var item in items)
        {
            col.Item().PaddingVertical(2).Row(row =>
            {
                // Label
                row.ConstantItem(150).AlignRight().PaddingRight(8)
                    .Text(TruncateText(item.Label, 30))
                    .FontSize(8).FontColor(Colors.Grey.Darken2);

                // Bar
                var barWidth = maxValue > 0 ? (float)item.Count / maxValue : 0;
                row.RelativeItem().Column(barCol =>
                {
                    barCol.Item().Height(16).Row(barRow =>
                    {
                        if (barWidth > 0)
                        {
                            barRow.RelativeItem((float)Math.Max(barWidth, 0.02))
                                .Background(Color.FromHex(SuccessColor));
                        }
                        if (barWidth < 1)
                        {
                            barRow.RelativeItem((float)(1 - barWidth))
                                .Background(Color.FromHex("#f1f5f9"));
                        }
                    });
                });

                // Value
                row.ConstantItem(50).PaddingLeft(8)
                    .Text($"{item.Count}").FontSize(8).Bold()
                    .FontColor(Color.FromHex(SuccessColor));
            });
        }
    }

    #endregion

    #region Pie Chart (legend-based)

    private static void RenderPieChart(ColumnDescriptor col, ReportSummaryDto? summary)
    {
        if (summary == null) return;

        var items = summary.FieldValueCounts
            .Select(kvp => new { Label = kvp.Key.Contains(':') ? kvp.Key.Split(':')[1] : kvp.Key, Count = kvp.Value })
            .OrderByDescending(x => x.Count)
            .Take(12)
            .ToList();

        var total = items.Sum(x => x.Count);
        if (total == 0)
        {
            col.Item().Padding(20).AlignCenter()
                .Text("Không có dữ liệu biểu đồ").FontColor(Colors.Grey.Medium);
            return;
        }

        var colors = new[] { "#10b981", "#3b82f6", "#f59e0b", "#ef4444", "#8b5cf6", "#ec4899", "#06b6d4", "#84cc16", "#f97316", "#6366f1", "#14b8a6", "#e11d48" };

        col.Item().Text("Phân bố tỷ lệ").FontSize(12).Bold().FontColor(Color.FromHex(PrimaryColor));
        col.Item().PaddingTop(8);

        // Stacked bar as pie representation
        col.Item().Height(32).Row(barRow =>
        {
            for (int i = 0; i < items.Count; i++)
            {
                var pct = (float)items[i].Count / total;
                barRow.RelativeItem(pct).Background(Color.FromHex(colors[i % colors.Length]));
            }
        });

        col.Item().PaddingTop(12);

        // Legend table
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(16); // Color
                cd.RelativeColumn(3);  // Label
                cd.RelativeColumn(1);  // Count
                cd.RelativeColumn(1);  // Percentage
            });

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var pct = (decimal)item.Count / total * 100;

                table.Cell().PaddingVertical(3).Width(12).Height(12)
                    .Background(Color.FromHex(colors[i % colors.Length]));
                table.Cell().PaddingVertical(3).PaddingLeft(8)
                    .Text(item.Label).FontSize(8);
                table.Cell().PaddingVertical(3).AlignRight()
                    .Text($"{item.Count}").FontSize(8).Bold();
                table.Cell().PaddingVertical(3).AlignRight()
                    .Text($"{pct:N1}%").FontSize(8).FontColor(Colors.Grey.Darken1);
            }
        });
    }

    #endregion

    #region Summary Grid

    private static void RenderSummaryGrid(ColumnDescriptor col, ReportSummaryDto? summary)
    {
        if (summary == null) return;

        var items = summary.FieldValueCounts
            .Select(kvp => new { Label = kvp.Key.Contains(':') ? kvp.Key.Split(':')[1] : kvp.Key, Count = kvp.Value })
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToList();

        col.Item().Text("Tổng hợp dữ liệu").FontSize(12).Bold().FontColor(Color.FromHex(PrimaryColor));
        col.Item().PaddingTop(8);

        // Grid layout — 4 items per row
        var rows = items.Chunk(4).ToList();
        foreach (var rowItems in rows)
        {
            col.Item().PaddingVertical(4).Row(row =>
            {
                foreach (var item in rowItems)
                {
                    row.RelativeItem().Padding(4)
                        .Border(1).BorderColor(Color.FromHex(BorderColor))
                        .Background(Color.FromHex(HeaderBg))
                        .Padding(12).Column(c =>
                        {
                            c.Item().AlignCenter()
                                .Text($"{item.Count}").FontSize(20).Bold()
                                .FontColor(Color.FromHex(SuccessColor));
                            c.Item().PaddingTop(4).AlignCenter()
                                .Text(TruncateText(item.Label, 25))
                                .FontSize(7).FontColor(Colors.Grey.Darken2);
                        });
                }
                // Fill remaining cells for alignment
                for (int i = rowItems.Length; i < 4; i++)
                {
                    row.RelativeItem();
                }
            });
        }
    }

    #endregion

    #region Helpers

    private static string FormatCellValue(string column, object? value)
    {
        if (value == null) return "-";

        if (column == "submittedAt" || column.EndsWith("At") || column.EndsWith("Date"))
        {
            if (value is DateTime dt)
                return dt.ToString("dd/MM/yyyy HH:mm");
            if (DateTime.TryParse(value.ToString(), out var parsed))
                return parsed.ToString("dd/MM/yyyy HH:mm");
        }

        if (column == "status")
        {
            return value.ToString() switch
            {
                "Draft" => "Bản nháp",
                "Submitted" => "Chờ duyệt",
                "Reviewed" => "Đã xem",
                "Approved" => "Đã duyệt",
                "Rejected" => "Từ chối",
                _ => value.ToString() ?? "-"
            };
        }

        var str = value.ToString() ?? "-";
        return TruncateText(str, 50);
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "-";
        return text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";
    }

    private static string GetReportTypeName(ReportType type) => type switch
    {
        ReportType.Table => "Bảng dữ liệu",
        ReportType.BarChart => "Biểu đồ cột",
        ReportType.LineChart => "Biểu đồ đường",
        ReportType.PieChart => "Biểu đồ tròn",
        ReportType.Summary => "Tổng hợp",
        _ => type.ToString()
    };

    private static PageSize ResolvePageSize(string size) => size.ToUpperInvariant() switch
    {
        "A5" => PageSizes.A5,
        "LETTER" => PageSizes.Letter,
        _ => PageSizes.A4
    };

    #endregion

    #region Band-Based PDF Rendering (Phase 2+3)

    /// <summary>
    /// Generates a PDF from a band-based visual report design.
    /// Iterates through bands (PageHeader, GroupHeader, Detail, GroupFooter, PageFooter)
    /// and renders each control at its absolute position within QuestPDF containers.
    /// </summary>
    private static byte[] GenerateBandBasedPdf(
        ReportTemplateDto template,
        List<Dictionary<string, object?>> data,
        ReportSummaryDto? summary,
        ReportDesignDto design,
        DateTime? filterFrom,
        DateTime? filterTo,
        SignatureContext? signatureContext = null,
        Dictionary<string, SignatureContext>? roleSignatures = null)
    {
        var pageWidth = design.PageWidth > 0 ? design.PageWidth : 800;
        var bands = design.Bands.Where(b => b.Visible).ToList();

        // Separate band categories
        var pageHeaders = bands.Where(b => b.Type == "pageHeader").ToList();
        var groupHeaders = bands.Where(b => b.Type == "groupHeader").OrderBy(b => b.GroupField).ToList();
        var detailBands = bands.Where(b => b.Type == "detail").ToList();
        var groupFooters = bands.Where(b => b.Type == "groupFooter").OrderByDescending(b => b.GroupField).ToList();
        var pageFooters = bands.Where(b => b.Type == "pageFooter").ToList();
        var reportHeaders = bands.Where(b => b.Type == "reportHeader").ToList();
        var reportFooters = bands.Where(b => b.Type == "reportFooter").ToList();

        // Build parameter values dictionary (use defaults)
        var paramValues = design.Parameters.ToDictionary(
            p => p.Name,
            p => (object?)(p.DefaultValue ?? ""));

        // For single-response reports, use the first row for header/footer field bindings
        var firstRow = data.Count > 0 ? data[0] : null;

        // Calculate maximum band height that fits in a single page's content area
        // A4 portrait = 842pt, landscape = 595pt height; margins = 30pt each side
        var isLandscapeCalc = pageWidth > 800;
        var pageHeightPt = isLandscapeCalc ? 595f : 842f;
        var headerTotalHeight = pageHeaders.Sum(b => (float)Math.Max(b.Height, 20));
        var footerTotalHeight = pageFooters.Sum(b => (float)Math.Max(b.Height, 20));
        var maxBandHeight = pageHeightPt - 60f - headerTotalHeight - footerTotalHeight;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                // Determine page orientation from design pageWidth
                // Portrait A4 has ~595pt width; template pageWidth > 800 suggests landscape
                var isLandscape = isLandscapeCalc;
                page.Size(isLandscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                // Page Header
                if (pageHeaders.Count > 0)
                {
                    page.Header().Column(col =>
                    {
                        foreach (var band in pageHeaders)
                        {
                            RenderBandContainer(col, band, firstRow, data, summary, paramValues, pageWidth, maxBandHeight, signatureContext, roleSignatures);
                        }
                    });
                }
                else
                {
                    // Fallback header
                    page.Header().Background(Color.FromHex(PrimaryColor)).Padding(12)
                        .Text(template.Name).FontSize(14).Bold().FontColor(Colors.White);
                }

                // Content
                page.Content().Column(col =>
                {
                    // Report headers (once)
                    foreach (var band in reportHeaders)
                    {
                        RenderBandContainer(col, band, firstRow, data, summary, paramValues, pageWidth, maxBandHeight, signatureContext, roleSignatures);
                    }

                    // Determine grouping
                    var groupField = groupHeaders.FirstOrDefault()?.GroupField;

                    if (!string.IsNullOrEmpty(groupField) && data.Count > 0)
                    {
                        // Grouped rendering
                        var groups = data
                            .GroupBy(r => r.GetValueOrDefault(groupField)?.ToString() ?? "-")
                            .ToList();

                        foreach (var grp in groups)
                        {
                            // Group header
                            foreach (var band in groupHeaders.Where(b => b.GroupField == groupField))
                            {
                                var groupRow = new Dictionary<string, object?>(grp.First())
                                {
                                    ["_groupKey"] = grp.Key,
                                    ["_groupCount"] = grp.Count()
                                };
                                RenderBandContainer(col, band, groupRow, data, summary, paramValues, pageWidth, maxBandHeight, signatureContext, roleSignatures);
                            }

                            // Detail rows
                            foreach (var row in grp)
                            {
                                foreach (var band in detailBands)
                                {
                                    RenderBandContainer(col, band, row, data, summary, paramValues, pageWidth, maxBandHeight, signatureContext, roleSignatures);
                                }
                            }

                            // Group footer
                            foreach (var band in groupFooters.Where(b => b.GroupField == groupField))
                            {
                                var groupData = grp.ToList();
                                var footerRow = new Dictionary<string, object?>
                                {
                                    ["_groupKey"] = grp.Key,
                                    ["_groupCount"] = grp.Count(),
                                    ["_groupData"] = groupData
                                };
                                RenderBandContainer(col, band, footerRow, data, summary, paramValues, pageWidth, maxBandHeight, signatureContext, roleSignatures);
                            }
                        }
                    }
                    else
                    {
                        // Non-grouped — render each data row in detail bands
                        foreach (var row in data)
                        {
                            foreach (var band in detailBands)
                            {
                                RenderBandContainer(col, band, row, data, summary, paramValues, pageWidth, maxBandHeight, signatureContext, roleSignatures);
                            }
                        }
                    }

                    // Report footers (once)
                    foreach (var band in reportFooters)
                    {
                        RenderBandContainer(col, band, firstRow, data, summary, paramValues, pageWidth, maxBandHeight, signatureContext, roleSignatures);
                    }
                });

                // Page Footer
                if (pageFooters.Count > 0)
                {
                    page.Footer().Column(col =>
                    {
                        foreach (var band in pageFooters)
                        {
                            RenderBandContainer(col, band, firstRow, data, summary, paramValues, pageWidth, maxBandHeight, signatureContext, roleSignatures);
                        }
                    });
                }
                else
                {
                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().AlignLeft()
                            .Text($"IVF Report — {template.Name}")
                            .FontSize(7).FontColor(Colors.Grey.Medium);
                        row.RelativeItem().AlignCenter().Text(text =>
                        {
                            text.Span("Trang ").FontSize(7).FontColor(Colors.Grey.Medium);
                            text.CurrentPageNumber().FontSize(7);
                            text.Span(" / ").FontSize(7).FontColor(Colors.Grey.Medium);
                            text.TotalPages().FontSize(7);
                        });
                    });
                }
            });
        });

        return document.GeneratePdf();
    }

    /// <summary>
    /// Renders a single band as a fixed-height container with absolutely-positioned controls.
    /// Uses QuestPDF Layers for control positioning.
    /// Horizontal scaling only: X and Width are scaled to fit A4 content width.
    /// Y, Height, FontSize, Padding, Border remain at original designer values.
    /// 
    /// Positioning: uses four-sided padding (left, top, right, bottom) to create an
    /// exact bounding box for each control. This is more reliable than PaddingLeft+Width
    /// because it eliminates any default alignment ambiguity.
    /// </summary>
    private static void RenderBandContainer(
        ColumnDescriptor col,
        ReportBandDto band,
        Dictionary<string, object?>? currentRow,
        List<Dictionary<string, object?>> allData,
        ReportSummaryDto? summary,
        Dictionary<string, object?> paramValues,
        int pageWidth,
        float maxBandHeight,
        SignatureContext? signatureContext = null,
        Dictionary<string, SignatureContext>? roleSignatures = null)
    {
        var bandHeight = (float)Math.Max(band.Height, 20);

        // Scale band vertically if it exceeds the available page content area
        var scaleY = 1.0f;
        if (bandHeight > maxBandHeight && maxBandHeight > 0)
        {
            scaleY = maxBandHeight / bandHeight;
            bandHeight = maxBandHeight;
        }

        // Scale factor for HORIZONTAL axis only
        // A4 portrait content = ~535pt (595-60), A4 landscape content = ~782pt (842-60)
        var pdfContentWidth = pageWidth > 800 ? 782.0f : 535.0f;
        var scaleX = pdfContentWidth / Math.Max(pageWidth, 1);

        // Use original band height (no vertical scaling)
        col.Item().Height(bandHeight).Layers(layers =>
        {
            // Primary layer with explicit size to anchor overlay positioning
            layers.PrimaryLayer().MinWidth(pdfContentWidth).MinHeight(bandHeight);

            // Control layers
            foreach (var ctrl in band.Controls)
            {
                var x = ctrl.X * scaleX;           // Scale X horizontally
                var y = (float)ctrl.Y * scaleY;    // Scale Y vertically if needed
                var w = Math.Max(ctrl.Width * scaleX, 10); // Scale Width horizontally
                var h = Math.Max((float)ctrl.Height * scaleY, 10);  // Scale Height vertically if needed

                // Skip controls positioned entirely outside the band boundaries
                if (y >= bandHeight || x >= pdfContentWidth)
                    continue;

                // Clamp dimensions to stay within band boundaries
                if (y + h > bandHeight) h = bandHeight - y;
                if (x + w > pdfContentWidth) w = pdfContentWidth - x;

                // Four-sided padding to create exact bounding box at (x, y, w, h)
                // This eliminates any default alignment ambiguity within the layer
                var pRight = Math.Max(pdfContentWidth - x - w, 0);
                var pBottom = Math.Max(bandHeight - y - h, 0);

                layers.Layer()
                    .PaddingLeft(x)
                    .PaddingTop(y)
                    .PaddingRight(pRight)
                    .PaddingBottom(pBottom)
                    .Element(container =>
                    {
                        // Handle line controls
                        if (ctrl.Type == "line")
                        {
                            var lineColor = ctrl.Style?.BorderColor ?? "#000000";
                            var lineWidth = Math.Max(ctrl.Style?.BorderWidth ?? 1, 0.5f);
                            container.PaddingTop(h / 2)
                                .LineHorizontal(lineWidth)
                                .LineColor(Color.FromHex(lineColor));
                            return;
                        }

                        // Handle signatureZone controls
                        if (ctrl.Type == "signatureZone")
                        {
                            // Per-role: look up in roleSignatures first, fall back to single signatureContext
                            SignatureContext? effectiveCtx = null;
                            if (roleSignatures != null && !string.IsNullOrEmpty(ctrl.SignatureRole)
                                && roleSignatures.TryGetValue(ctrl.SignatureRole, out var roleCtx))
                            {
                                effectiveCtx = roleCtx;
                            }
                            else
                            {
                                effectiveCtx = signatureContext;
                            }
                            RenderSignatureZone(container, ctrl, w, h, effectiveCtx);
                            return;
                        }

                        // Handle pageNumber with QuestPDF actual page numbers
                        if (ctrl.Type == "pageNumber")
                        {
                            ApplyControlStyle(container, ctrl.Style)
                                .Text(text =>
                                {
                                    var template = ctrl.Text ?? "Trang {page}";
                                    var style = ctrl.Style;
                                    var fontSize = style?.FontSize ?? 9f;
                                    var fontColor = !string.IsNullOrEmpty(style?.Color)
                                        ? Color.FromHex(style.Color)
                                        : Colors.Black;

                                    // Split text around {page} and {totalPages} placeholders
                                    var parts = Regex.Split(template, @"(\{page\}|\{totalPages\})");
                                    foreach (var part in parts)
                                    {
                                        if (part == "{page}")
                                        {
                                            var span = text.CurrentPageNumber().FontSize(fontSize).FontColor(fontColor);
                                            if (style?.FontWeight == "bold") span.Bold();
                                        }
                                        else if (part == "{totalPages}")
                                        {
                                            var span = text.TotalPages().FontSize(fontSize).FontColor(fontColor);
                                            if (style?.FontWeight == "bold") span.Bold();
                                        }
                                        else if (!string.IsNullOrEmpty(part))
                                        {
                                            var span = text.Span(part).FontSize(fontSize).FontColor(fontColor);
                                            if (style?.FontWeight == "bold") span.Bold();
                                            if (style?.FontStyle == "italic") span.Italic();
                                        }
                                    }
                                });
                            return;
                        }

                        // Default: render as text with original font sizes
                        ApplyControlStyle(container, ctrl.Style)
                            .Text(text =>
                            {
                                var resolvedText = ResolveControlText(ctrl, currentRow, allData, summary, paramValues);
                                var style = ctrl.Style;
                                var span = text.Span(resolvedText);

                                if (style != null)
                                {
                                    span.FontSize(style.FontSize ?? 9f);
                                    if (style.FontWeight == "bold") span.Bold();
                                    if (style.FontStyle == "italic") span.Italic();
                                    if (!string.IsNullOrEmpty(style.Color)) span.FontColor(Color.FromHex(style.Color));
                                }
                                else
                                {
                                    span.FontSize(9);
                                }
                            });
                    });
            }
        });
    }

    /// <summary>
    /// Resolves the display text for a control based on its type and data context.
    /// </summary>
    private static string ResolveControlText(
        ReportControlDto ctrl,
        Dictionary<string, object?>? row,
        List<Dictionary<string, object?>> allData,
        ReportSummaryDto? summary,
        Dictionary<string, object?> paramValues)
    {
        // Use dataField as fallback for fieldKey (frontend uses dataField)
        var effectiveFieldKey = ctrl.FieldKey ?? ctrl.DataField;

        return ctrl.Type switch
        {
            "label" => ctrl.Text ?? "",
            "field" => ResolveFieldValue(effectiveFieldKey, ctrl.Format, row),
            "expression" => EvaluateBackendExpression(ctrl.Expression ?? "", row, allData, summary, paramValues),
            "pageNumber" => "#",  // QuestPDF handles actual page numbers via text.CurrentPageNumber()
            "totalPages" => "#",
            "currentDate" => DateTime.Now.ToString(ctrl.Format ?? "dd/MM/yyyy"),
            "checkbox" => row?.GetValueOrDefault(effectiveFieldKey ?? "")?.ToString()?.ToLower() == "true" ? "☑" : "☐",
            "barcode" => ctrl.BarcodeValue ?? effectiveFieldKey ?? "BARCODE",
            "signatureZone" => ctrl.Text ?? "Chữ ký",
            _ => ctrl.Text ?? ""
        };
    }

    /// <summary>
    /// Resolves a field value from the data row, applying optional date formatting.
    /// </summary>
    private static string ResolveFieldValue(string? fieldKey, string? format, Dictionary<string, object?>? row)
    {
        if (string.IsNullOrEmpty(fieldKey) || row == null) return $"[{fieldKey}]";

        var value = row.GetValueOrDefault(fieldKey);
        if (value == null) return "";

        // Apply date formatting
        if (!string.IsNullOrEmpty(format) && (format.Contains("dd") || format.Contains("MM") || format.Contains("yyyy")))
        {
            if (value is DateTime dt)
                return dt.ToString(format);
            if (DateTime.TryParse(value.ToString(), out var parsed))
                return parsed.ToString(format);
        }

        // Format decimals cleanly (remove trailing zeros: 23.000000 → 23)
        if (value is decimal dec)
            return dec % 1 == 0 ? dec.ToString("0") : dec.ToString("0.##");

        return value.ToString() ?? "";
    }

    /// <summary>
    /// Applies style properties (background, border, padding) to a QuestPDF container.
    /// Returns the styled container for further chaining.
    /// Padding is applied horizontally only; vertical centering (AlignMiddle) ensures
    /// text at the same Y row aligns regardless of padding differences.
    /// </summary>
    private static IContainer ApplyControlStyle(IContainer container, ReportControlStyleDto? style)
    {
        if (style == null) return container.AlignMiddle();

        if (!string.IsNullOrEmpty(style.BackgroundColor))
            container = container.Background(Color.FromHex(style.BackgroundColor));

        if (style.BorderWidth.HasValue && style.BorderWidth > 0)
        {
            var borderColor = !string.IsNullOrEmpty(style.BorderColor)
                ? Color.FromHex(style.BorderColor)
                : Colors.Grey.Medium;
            container = container.Border(style.BorderWidth.Value).BorderColor(borderColor);
        }

        if (style.Padding.HasValue && style.Padding > 0)
            container = container.PaddingHorizontal(style.Padding.Value);

        // Vertical centering: ensures consistent text baseline across controls with different padding
        container = container.AlignMiddle();

        // Text alignment (horizontal)
        if (!string.IsNullOrEmpty(style.TextAlign))
        {
            container = style.TextAlign switch
            {
                "center" => container.AlignCenter(),
                "right" => container.AlignRight(),
                _ => container
            };
        }

        return container;
    }

    /// <summary>
    /// Backend expression evaluator matching the frontend evaluateExpression().
    /// Supports: [Data.fieldKey], [Param.name], bare field names, Count(), Sum(), Avg(), Min(), Max(),
    /// Upper(), Lower(), Iif(condition, trueVal, falseVal) with >=, <=, >, <, ==, != operators,
    /// nested Iif(), Format(), string concatenation with +.
    /// </summary>
    private static string EvaluateBackendExpression(
        string expression,
        Dictionary<string, object?>? row,
        List<Dictionary<string, object?>> allData,
        ReportSummaryDto? summary,
        Dictionary<string, object?> paramValues)
    {
        if (string.IsNullOrWhiteSpace(expression)) return "";

        try
        {
            var result = expression;

            // Replace [Data.fieldKey] references
            result = Regex.Replace(result, @"\[Data\.(\w+)\]", match =>
            {
                var key = match.Groups[1].Value;
                return row?.GetValueOrDefault(key)?.ToString() ?? "";
            });

            // Replace [Param.name] references
            result = Regex.Replace(result, @"\[Param\.(\w+)\]", match =>
            {
                var key = match.Groups[1].Value;
                return paramValues.GetValueOrDefault(key)?.ToString() ?? "";
            });

            // Replace bare field names with their values (before Iif evaluation)
            // This allows expressions like: Iif(volume >= 1.4, "Bình thường", "Thấp")
            if (row != null)
            {
                // Sort by key length descending to avoid partial replacements
                var sortedKeys = row.Keys.OrderByDescending(k => k.Length).ToList();
                foreach (var key in sortedKeys)
                {
                    if (string.IsNullOrEmpty(key)) continue;
                    // Only replace bare word references (not inside quotes or already replaced)
                    result = Regex.Replace(result, $@"(?<![""'\w]){Regex.Escape(key)}(?![""'\w])", _ =>
                    {
                        var val = row.GetValueOrDefault(key);
                        if (val is decimal d)
                            return d % 1 == 0 ? d.ToString("0") : d.ToString("0.##");
                        return val?.ToString() ?? "";
                    });
                }
            }

            // Aggregate functions: Count(), Sum(field), Avg(field), Min(field), Max(field)
            result = Regex.Replace(result, @"Count\(\)", _ => allData.Count.ToString());

            result = Regex.Replace(result, @"Sum\((\w+)\)", match =>
            {
                var field = match.Groups[1].Value;
                var sum = allData.Sum(r =>
                    decimal.TryParse(r.GetValueOrDefault(field)?.ToString(), out var v) ? v : 0);
                return sum.ToString("N2");
            });

            result = Regex.Replace(result, @"Avg\((\w+)\)", match =>
            {
                var field = match.Groups[1].Value;
                var nums = allData
                    .Select(r => decimal.TryParse(r.GetValueOrDefault(field)?.ToString(), out var v) ? (decimal?)v : null)
                    .Where(v => v.HasValue)
                    .ToList();
                return nums.Count > 0 ? (nums.Sum()!.Value / nums.Count).ToString("N2") : "0";
            });

            result = Regex.Replace(result, @"Min\((\w+)\)", match =>
            {
                var field = match.Groups[1].Value;
                var nums = allData
                    .Select(r => decimal.TryParse(r.GetValueOrDefault(field)?.ToString(), out var v) ? (decimal?)v : null)
                    .Where(v => v.HasValue)
                    .ToList();
                return nums.Count > 0 ? nums.Min()!.Value.ToString("N2") : "0";
            });

            result = Regex.Replace(result, @"Max\((\w+)\)", match =>
            {
                var field = match.Groups[1].Value;
                var nums = allData
                    .Select(r => decimal.TryParse(r.GetValueOrDefault(field)?.ToString(), out var v) ? (decimal?)v : null)
                    .Where(v => v.HasValue)
                    .ToList();
                return nums.Count > 0 ? nums.Max()!.Value.ToString("N2") : "0";
            });

            // Upper() / Lower()
            result = Regex.Replace(result, @"Upper\(([^)]+)\)", m => m.Groups[1].Value.ToUpperInvariant());
            result = Regex.Replace(result, @"Lower\(([^)]+)\)", m => m.Groups[1].Value.ToLowerInvariant());

            // Evaluate Iif() — process innermost first for nesting support
            result = EvaluateAllIif(result);

            // String concatenation with +
            if (result.Contains('+') && !decimal.TryParse(result, out _))
            {
                var parts = result.Split('+').Select(p => p.Trim().Trim('"', '\'')).ToArray();
                result = string.Join("", parts);
            }

            return result;
        }
        catch
        {
            return expression;
        }
    }

    /// <summary>
    /// Recursively evaluates all Iif() expressions, starting from the innermost.
    /// Supports: Iif(a >= b, "trueVal", "falseVal") with operators: >=, <=, >, <, ==, !=
    /// Also supports nested: Iif(a >= b, Iif(a <= c, "ok", "high"), "low")
    /// </summary>
    private static string EvaluateAllIif(string expr)
    {
        const int maxIterations = 20;
        for (int i = 0; i < maxIterations; i++)
        {
            // Find innermost Iif( ... ) — one that has no nested Iif inside
            var match = Regex.Match(expr, @"Iif\(([^()]*)\)", RegexOptions.IgnoreCase);
            if (!match.Success) break;

            var innerArgs = match.Groups[1].Value;
            var resolved = EvaluateSingleIif(innerArgs);
            expr = expr[..match.Index] + resolved + expr[(match.Index + match.Length)..];
        }
        return expr;
    }

    /// <summary>
    /// Evaluates a single Iif argument string: "condition, trueValue, falseValue"
    /// Splits respecting quoted strings.
    /// </summary>
    private static string EvaluateSingleIif(string args)
    {
        var parts = SplitRespectingQuotes(args, ',');
        if (parts.Count < 3) return args;

        var condition = parts[0].Trim();
        var trueVal = parts[1].Trim().Trim('"', '\'');
        var falseVal = parts[2].Trim().Trim('"', '\'');

        return EvaluateCondition(condition) ? trueVal : falseVal;
    }

    /// <summary>
    /// Evaluates a comparison condition string like "7.5 >= 7.2" or "Draft == Draft".
    /// Supports operators: >=, <=, !=, ==, >, <
    /// </summary>
    private static bool EvaluateCondition(string condition)
    {
        // Try each operator (order matters: >= before >, <= before <)
        string[] operators = [">=", "<=", "!=", "==", ">", "<"];
        foreach (var op in operators)
        {
            var idx = condition.IndexOf(op, StringComparison.Ordinal);
            if (idx < 0) continue;

            var left = condition[..idx].Trim().Trim('"', '\'');
            var right = condition[(idx + op.Length)..].Trim().Trim('"', '\'');

            if (decimal.TryParse(left, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lNum)
                && decimal.TryParse(right, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rNum))
            {
                return op switch
                {
                    ">=" => lNum >= rNum,
                    "<=" => lNum <= rNum,
                    "!=" => lNum != rNum,
                    "==" => lNum == rNum,
                    ">" => lNum > rNum,
                    "<" => lNum < rNum,
                    _ => false
                };
            }

            // String comparison
            return op switch
            {
                "==" => string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
                "!=" => !string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }

        // No operator found — treat as truthy check
        return !string.IsNullOrWhiteSpace(condition) && condition != "0" && condition.ToLower() != "false";
    }

    /// <summary>
    /// Splits a string by a delimiter while respecting quoted substrings.
    /// </summary>
    private static List<string> SplitRespectingQuotes(string input, char delimiter)
    {
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuote = false;
        var quoteChar = '"';
        var depth = 0;

        foreach (var ch in input)
        {
            if (!inQuote && (ch == '"' || ch == '\''))
            {
                inQuote = true;
                quoteChar = ch;
                current.Append(ch);
            }
            else if (inQuote && ch == quoteChar)
            {
                inQuote = false;
                current.Append(ch);
            }
            else if (!inQuote && ch == '(')
            {
                depth++;
                current.Append(ch);
            }
            else if (!inQuote && ch == ')')
            {
                depth--;
                current.Append(ch);
            }
            else if (!inQuote && depth == 0 && ch == delimiter)
            {
                parts.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        if (current.Length > 0)
            parts.Add(current.ToString());

        return parts;
    }

    /// <summary>
    /// Renders a signature zone control in the PDF.
    /// If a SignatureContext is provided (sign=true), renders the handwritten signature image
    /// with signer name and date. Otherwise renders a placeholder with dotted line.
    /// 
    /// The container is already sized and positioned by the parent RenderBandContainer.
    /// Content is rendered as a simple vertical stack that fits within the bounding box.
    /// </summary>
    private static void RenderSignatureZone(
        IContainer container,
        ReportControlDto ctrl,
        float width,
        float height,
        SignatureContext? signatureContext)
    {
        var roleLabel = ctrl.Text ?? "Chữ ký";
        var hasSignature = signatureContext?.SignatureImageBytes != null
                           && signatureContext.SignatureImageBytes.Length > 0;
        // All signatureZone controls with a signatureContext get the signature
        var shouldSign = hasSignature && (
            string.IsNullOrEmpty(ctrl.SignatureRole)
            || ctrl.SignatureRole == "current_user"
            || ctrl.SignatureRole == "technician"
            || ctrl.SignatureRole == "doctor"
            || ctrl.SignatureRole == "department_head"
            || ctrl.SignatureRole == "director");

        var fontSize = ctrl.Style?.FontSize ?? 9;
        var fontColor = !string.IsNullOrEmpty(ctrl.Style?.Color)
            ? Color.FromHex(ctrl.Style!.Color!)
            : Colors.Grey.Darken2;

        // Render using a Row with a single RelativeItem to get a vertical stack
        // that fills the available space from the parent's bounding box.
        container.Column(col =>
        {
            // ── Title ──
            col.Item().AlignCenter()
                .Text(roleLabel)
                .FontSize(fontSize).Bold().FontColor(fontColor);

            if (shouldSign)
            {
                // ── Signature image (constrained to not overflow) ──
                col.Item().AlignCenter()
                    .MaxWidth(width * 0.8f)
                    .MaxHeight(Math.Max(height - 30, 10))
                    .Image(signatureContext!.SignatureImageBytes!)
                    .FitArea();

                // ── Signer name ──
                if (!string.IsNullOrEmpty(signatureContext.SignerName))
                {
                    col.Item().AlignCenter()
                        .Text(signatureContext.SignerName)
                        .FontSize(7).Bold();
                }
            }
            else
            {
                // ── Placeholder ──
                col.Item().AlignCenter()
                    .PaddingTop(Math.Max(height - 30, 2))
                    .Text("(Ký và ghi rõ họ tên)")
                    .FontSize(7).FontColor(Colors.Grey.Medium).Italic();
            }
        });
    }

    #endregion
}
