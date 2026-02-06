namespace IVF.Application.Common.Interfaces;

public interface IFlowSeeder
{
    Task SeedFlowDataAsync(CancellationToken cancellationToken = default);
}
