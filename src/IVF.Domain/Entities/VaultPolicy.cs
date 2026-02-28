using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class VaultPolicy : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string PathPattern { get; private set; } = "*";
    public string[] Capabilities { get; private set; } = [];
    public Guid? CreatedBy { get; private set; }

    private VaultPolicy() { }

    public static VaultPolicy Create(
        string name,
        string pathPattern,
        string[] capabilities,
        string? description = null,
        Guid? createdBy = null)
    {
        return new VaultPolicy
        {
            Name = name,
            PathPattern = pathPattern,
            Capabilities = capabilities,
            Description = description,
            CreatedBy = createdBy
        };
    }

    public void Update(string pathPattern, string[] capabilities, string? description = null)
    {
        PathPattern = pathPattern;
        Capabilities = capabilities;
        if (description is not null)
            Description = description;
        SetUpdated();
    }

    public static readonly string[] AllCapabilities =
        ["read", "create", "update", "delete", "list", "sudo"];
}
