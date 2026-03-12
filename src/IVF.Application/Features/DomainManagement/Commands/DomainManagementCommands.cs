using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using MediatR;

namespace IVF.Application.Features.DomainManagement.Commands;

// ── Sync Caddy Config ───────────────────────────────────────────────────────
public record SyncCaddyConfigCommand : IRequest<Result<CaddySyncResult>>;

public class SyncCaddyConfigHandler : IRequestHandler<SyncCaddyConfigCommand, Result<CaddySyncResult>>
{
    private readonly ICaddyConfigService _caddyConfigService;

    public SyncCaddyConfigHandler(ICaddyConfigService caddyConfigService)
    {
        _caddyConfigService = caddyConfigService;
    }

    public async Task<Result<CaddySyncResult>> Handle(SyncCaddyConfigCommand request, CancellationToken ct)
    {
        var result = await _caddyConfigService.SyncConfigAsync(ct);
        return result.Success
            ? Result<CaddySyncResult>.Success(result)
            : Result<CaddySyncResult>.Failure(result.Message);
    }
}
