using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Documents.Commands;
using IVF.Application.Features.Forms.Queries;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Services;

/// <summary>
/// Encapsulates the full flow: generate PDF → embed signature images → sign via SignServer → store to MinIO.
/// Called from:
///   - DocumentSignatureEndpoints (auto-generate when fully signed)
///   - FormEndpoints (manual "Ký số" button click)
/// </summary>
public class SignedPdfGenerationService(
    IMediator mediator,
    IvfDbContext db,
    IDigitalSigningService signingService,
    ILogger<SignedPdfGenerationService> logger)
{
    /// <summary>
    /// Generate a signed PDF for a form response that has all required signatures,
    /// then upload to MinIO and create/update PatientDocument record.
    /// Returns null if prerequisites are not met (no report template, no patient, etc.).
    /// </summary>
    public async Task<GenerationResult?> GenerateAndStoreAsync(
        Guid formResponseId,
        CancellationToken ct = default)
    {
        try
        {
            // 1. Load form response
            var response = await mediator.Send(new GetFormResponseByIdQuery(formResponseId), ct);
            if (response == null || !response.PatientId.HasValue)
            {
                logger.LogWarning("GenerateAndStore: FormResponse {Id} not found or has no patient", formResponseId);
                return null;
            }

            // 2. Load form template
            var template = await mediator.Send(new GetFormTemplateByIdQuery(response.FormTemplateId), ct);
            if (template == null) return null;

            // 3. Find band-based report template
            var reportTemplates = await mediator.Send(new GetReportTemplatesQuery(response.FormTemplateId), ct);
            var reportTemplate = reportTemplates.FirstOrDefault(rt =>
                !string.IsNullOrWhiteSpace(rt.ConfigurationJson)
                && rt.ConfigurationJson.Contains("\"bands\""));

            if (reportTemplate == null)
            {
                logger.LogInformation("No band-based report template for FormTemplate {Id}, skipping auto-generation",
                    response.FormTemplateId);
                return null;
            }

            // 4. Build data row from field values
            var dataRow = new Dictionary<string, object?>
            {
                ["patientName"] = response.PatientName ?? "",
                ["submittedAt"] = response.SubmittedAt?.ToString("o") ?? response.CreatedAt.ToString("o"),
                ["status"] = response.Status.ToString()
            };
            if (response.FieldValues != null)
            {
                foreach (var fv in response.FieldValues)
                {
                    var key = fv.FieldKey ?? fv.FormFieldId.ToString();
                    object? value = fv.NumericValue.HasValue ? fv.NumericValue.Value
                        : fv.DateValue.HasValue ? fv.DateValue.Value
                        : fv.BooleanValue.HasValue ? fv.BooleanValue.Value
                        : !string.IsNullOrEmpty(fv.JsonValue) ? fv.JsonValue
                        : fv.TextValue;
                    dataRow[key] = value;
                }
            }

            var reportData = new ReportDataDto(reportTemplate, [dataRow], null);

            // 5. Load all DocumentSignature records
            var docSignatures = await db.DocumentSignatures
                .Include(d => d.User)
                .Where(d => d.FormResponseId == formResponseId && !d.IsDeleted)
                .OrderBy(d => d.SignedAt)
                .ToListAsync(ct);

            if (docSignatures.Count == 0)
            {
                logger.LogInformation("No signatures for FormResponse {Id}, skipping", formResponseId);
                return null;
            }

            // 6. Build per-role signature contexts
            var roleSignatures = new Dictionary<string, ReportPdfService.SignatureContext>();
            var signerWorkers = new List<(string WorkerName, string? SignerName)>();

            var signerUserIds = docSignatures.Select(d => d.UserId).Distinct().ToList();
            var signerSigs = await db.UserSignatures
                .Include(s => s.User)
                .Where(s => signerUserIds.Contains(s.UserId) && !s.IsDeleted && s.IsActive)
                .ToListAsync(ct);
            var sigLookup = signerSigs.ToDictionary(s => s.UserId);

            foreach (var ds in docSignatures)
            {
                if (sigLookup.TryGetValue(ds.UserId, out var sig)
                    && !string.IsNullOrEmpty(sig.SignatureImageBase64))
                {
                    var base64 = sig.SignatureImageBase64;
                    if (base64.Contains(','))
                        base64 = base64[(base64.IndexOf(',') + 1)..];

                    roleSignatures[ds.SignatureRole] = new ReportPdfService.SignatureContext(
                        SignerName: ds.User?.FullName ?? sig.User?.FullName,
                        SignatureImageBytes: Convert.FromBase64String(base64),
                        SignedDate: ds.SignedAt.ToString("dd/MM/yyyy"));

                    var workerName = sig.CertStatus == CertificateStatus.Active
                        && !string.IsNullOrEmpty(sig.WorkerName)
                        ? sig.WorkerName
                        : null;
                    signerWorkers.Add((workerName ?? "__default__", ds.User?.FullName));
                }
            }

            // 7. Generate PDF with embedded signature images
            var pdfBytes = ReportPdfService.GenerateReportPdf(reportData,
                roleSignatures: roleSignatures);

            // 8. Cryptographic digital signing via SignServer
            if (signingService.IsEnabled && signerWorkers.Count > 0)
            {
                var uniqueWorkers = signerWorkers
                    .GroupBy(w => w.WorkerName)
                    .Select(g => g.First())
                    .ToList();

                foreach (var (workerName, signerName) in uniqueWorkers)
                {
                    var metadata = new SigningMetadata(
                        Reason: $"Xác nhận phiếu: {template.Name}",
                        Location: "IVF Clinic",
                        SignerName: signerName ?? response.PatientName);

                    if (workerName != "__default__")
                    {
                        pdfBytes = await signingService.SignPdfWithUserAsync(
                            pdfBytes, workerName, metadata, cancellationToken: ct);
                    }
                    else
                    {
                        pdfBytes = await signingService.SignPdfAsync(pdfBytes, metadata, ct);
                    }
                }
            }

            // 9. Get patient code
            var patient = await db.Patients
                .AsNoTracking()
                .Where(p => p.Id == response.PatientId.Value && !p.IsDeleted)
                .Select(p => new { p.PatientCode })
                .FirstOrDefaultAsync(ct);
            var patientCode = patient?.PatientCode ?? response.PatientId.Value.ToString("N")[..8];

            var signerNamesList = string.Join(", ",
                signerWorkers.Select(w => w.SignerName).Where(n => !string.IsNullOrEmpty(n)));

            // 10. Store in MinIO via MediatR command
            var storeResult = await mediator.Send(new StoreSignedFormPdfCommand(
                FormResponseId: formResponseId,
                PatientId: response.PatientId!.Value,
                PatientCode: patientCode,
                TemplateName: template.Name,
                SignedPdfBytes: pdfBytes,
                SignerNames: signerNamesList,
                FormTemplateCode: template.Code,
                CycleId: response.CycleId), ct);

            if (!storeResult.IsSuccess)
            {
                logger.LogError("Failed to store signed PDF for FormResponse {Id}: {Error}",
                    formResponseId, storeResult.Error);
                return new GenerationResult(false, storeResult.Error, null, 0);
            }

            logger.LogInformation(
                "Auto-generated and stored signed PDF for FormResponse {Id} ({Size} bytes, {Signers})",
                formResponseId, pdfBytes.Length, signerNamesList);

            return new GenerationResult(true, null, storeResult.Value?.ObjectKey, pdfBytes.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in GenerateAndStore for FormResponse {Id}", formResponseId);
            return new GenerationResult(false, ex.Message, null, 0);
        }
    }

    public record GenerationResult(
        bool Success,
        string? Error,
        string? ObjectKey,
        long FileSizeBytes);
}
