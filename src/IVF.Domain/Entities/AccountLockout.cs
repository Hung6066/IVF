using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class AccountLockout : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public DateTime LockedAt { get; private set; }
    public DateTime UnlocksAt { get; private set; }
    public int FailedAttempts { get; private set; }
    public string? LockedBy { get; private set; }
    public bool IsManualLock { get; private set; }

    private AccountLockout() { }

    public static AccountLockout Create(
        Guid userId,
        string username,
        string reason,
        int durationMinutes,
        int failedAttempts = 0,
        string? lockedBy = null,
        bool isManualLock = false)
    {
        return new AccountLockout
        {
            UserId = userId,
            Username = username,
            Reason = reason,
            LockedAt = DateTime.UtcNow,
            UnlocksAt = DateTime.UtcNow.AddMinutes(durationMinutes),
            FailedAttempts = failedAttempts,
            LockedBy = lockedBy,
            IsManualLock = isManualLock
        };
    }

    public bool IsCurrentlyLocked() => !IsDeleted && UnlocksAt > DateTime.UtcNow;

    public void Unlock()
    {
        UnlocksAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void IncrementFailedAttempts()
    {
        FailedAttempts++;
        SetUpdated();
    }
}
