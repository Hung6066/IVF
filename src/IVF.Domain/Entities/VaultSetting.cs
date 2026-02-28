namespace IVF.Domain.Entities;

/// <summary>
/// Key-value settings for the vault (no soft-delete, no BaseEntity).
/// </summary>
public class VaultSetting
{
    public string Key { get; private set; } = string.Empty;
    public string ValueJson { get; private set; } = "{}";
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; private set; }

    private VaultSetting() { }

    public static VaultSetting Create(string key, string valueJson)
    {
        return new VaultSetting
        {
            Key = key,
            ValueJson = valueJson
        };
    }

    public void Update(string valueJson)
    {
        ValueJson = valueJson;
        UpdatedAt = DateTime.UtcNow;
    }
}
