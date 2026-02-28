using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class EncryptionConfig : BaseEntity
{
    public string TableName { get; private set; } = string.Empty;
    public string[] EncryptedFields { get; private set; } = [];
    public string DekPurpose { get; private set; } = "data"; // data|session|api|backup
    public bool IsEnabled { get; private set; } = true;
    public bool IsDefault { get; private set; }
    public string? Description { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    private EncryptionConfig() { }

    public static EncryptionConfig Create(
        string tableName,
        string[] encryptedFields,
        string dekPurpose = "data",
        string? description = null,
        bool isDefault = false,
        Guid? createdBy = null)
    {
        return new EncryptionConfig
        {
            TableName = tableName,
            EncryptedFields = encryptedFields,
            DekPurpose = dekPurpose,
            IsEnabled = true,
            IsDefault = isDefault,
            Description = description,
            CreatedBy = createdBy,
        };
    }

    public void Update(string[] encryptedFields, string dekPurpose, string? description, Guid? updatedBy = null)
    {
        EncryptedFields = encryptedFields;
        DekPurpose = dekPurpose;
        Description = description;
        UpdatedBy = updatedBy;
        SetUpdated();
    }

    public void SetEnabled(bool enabled, Guid? updatedBy = null)
    {
        IsEnabled = enabled;
        UpdatedBy = updatedBy;
        SetUpdated();
    }
}
