using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Tracks each user's signing action on a form response.
/// Multiple users can sign the same document with different roles.
/// When downloading the signed PDF, all signatures are applied incrementally
/// via SignServer, producing a PDF with multiple valid PAdES signatures.
/// </summary>
public class DocumentSignature : BaseEntity
{
    /// <summary>FK to the FormResponse being signed.</summary>
    public Guid FormResponseId { get; private set; }

    /// <summary>FK to the User who signed.</summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// The role this signature represents (e.g. "technician", "department_head", "doctor", "director").
    /// Maps to signatureZone control's signatureRole in the report designer.
    /// </summary>
    public string SignatureRole { get; private set; } = string.Empty;

    /// <summary>When the user signed the document.</summary>
    public DateTime SignedAt { get; private set; }

    /// <summary>Optional reason or notes for this signature.</summary>
    public string? Notes { get; private set; }

    // Navigation
    public FormResponse FormResponse { get; private set; } = null!;
    public User User { get; private set; } = null!;

    private DocumentSignature() { }

    public static DocumentSignature Create(
        Guid formResponseId,
        Guid userId,
        string signatureRole,
        string? notes = null)
    {
        return new DocumentSignature
        {
            Id = Guid.NewGuid(),
            FormResponseId = formResponseId,
            UserId = userId,
            SignatureRole = signatureRole,
            SignedAt = DateTime.UtcNow,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Revoke(string? reason = null)
    {
        Notes = reason ?? "Đã thu hồi chữ ký";
        MarkAsDeleted();
    }
}
