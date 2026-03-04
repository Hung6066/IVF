namespace IVF.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Username { get; }
    string? Role { get; }
    string? IpAddress { get; }
    Guid? TenantId { get; }
    bool IsPlatformAdmin { get; }
}
