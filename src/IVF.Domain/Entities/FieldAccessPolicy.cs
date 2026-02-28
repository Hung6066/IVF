using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class FieldAccessPolicy : BaseEntity
{
    public string TableName { get; private set; } = string.Empty;
    public string FieldName { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public string AccessLevel { get; private set; } = "none"; // full|partial|masked|none
    public string MaskPattern { get; private set; } = "********";
    public int PartialLength { get; private set; } = 5;
    public string? Description { get; private set; }
    public Guid? CreatedBy { get; private set; }

    private FieldAccessPolicy() { }

    public static FieldAccessPolicy Create(
        string tableName,
        string fieldName,
        string role,
        string accessLevel,
        string? maskPattern = null,
        int partialLength = 5,
        string? description = null,
        Guid? createdBy = null)
    {
        return new FieldAccessPolicy
        {
            TableName = tableName,
            FieldName = fieldName,
            Role = role,
            AccessLevel = accessLevel,
            MaskPattern = maskPattern ?? "********",
            PartialLength = partialLength,
            Description = description,
            CreatedBy = createdBy,
        };
    }

    public void Update(string accessLevel, string? maskPattern, int partialLength, string? description)
    {
        AccessLevel = accessLevel;
        MaskPattern = maskPattern ?? MaskPattern;
        PartialLength = partialLength;
        Description = description;
        SetUpdated();
    }
}
