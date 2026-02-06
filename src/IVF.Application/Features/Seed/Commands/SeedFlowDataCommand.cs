using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using MediatR;

namespace IVF.Application.Features.Seed.Commands;

public record SeedFlowDataCommand : IRequest<Result>;

public class SeedFlowDataHandler : IRequestHandler<SeedFlowDataCommand, Result>
{
    private readonly IFlowSeeder _seeder;

    public SeedFlowDataHandler(IFlowSeeder seeder)
    {
        _seeder = seeder;
    }

    public async Task<Result> Handle(SeedFlowDataCommand request, CancellationToken cancellationToken)
    {
        await _seeder.SeedFlowDataAsync(cancellationToken);
        return Result.Success();
    }
}
