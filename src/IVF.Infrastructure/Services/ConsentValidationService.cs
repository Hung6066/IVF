using IVF.Application.Common.Interfaces;

namespace IVF.Infrastructure.Services;

public class ConsentValidationService : IConsentValidationService
{
    private readonly IEnterpriseUserRepository _repo;

    public ConsentValidationService(IEnterpriseUserRepository repo)
    {
        _repo = repo;
    }

    public async Task<bool> HasValidConsentAsync(Guid userId, string consentType, CancellationToken ct = default)
    {
        var consents = await _repo.GetUserConsentsAsync(userId, ct);
        return consents.Any(c => c.ConsentType == consentType && c.IsValid());
    }

    public async Task<List<string>> GetMissingConsentsAsync(Guid userId, IEnumerable<string> requiredTypes, CancellationToken ct = default)
    {
        var consents = await _repo.GetUserConsentsAsync(userId, ct);
        var validTypes = consents.Where(c => c.IsValid()).Select(c => c.ConsentType).ToHashSet();
        return requiredTypes.Where(t => !validTypes.Contains(t)).ToList();
    }
}
