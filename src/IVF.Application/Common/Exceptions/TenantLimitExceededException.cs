namespace IVF.Application.Common.Exceptions;

public class TenantLimitExceededException : Exception
{
    public string LimitType { get; }
    public int CurrentCount { get; }
    public int MaxAllowed { get; }

    public TenantLimitExceededException(string limitType, int currentCount, int maxAllowed)
        : base($"Đã vượt giới hạn {limitType}: hiện tại {currentCount}/{maxAllowed}")
    {
        LimitType = limitType;
        CurrentCount = currentCount;
        MaxAllowed = maxAllowed;
    }
}
