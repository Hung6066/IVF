using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Procedures.Commands;
using MediatR;

namespace IVF.Application.Features.Procedures.Queries;

// === Get by ID ===

public record GetProcedureByIdQuery(Guid Id) : IRequest<Result<ProcedureDto>>;

public class GetProcedureByIdHandler(IProcedureRepository repo)
    : IRequestHandler<GetProcedureByIdQuery, Result<ProcedureDto>>
{
    public async Task<Result<ProcedureDto>> Handle(GetProcedureByIdQuery request, CancellationToken ct)
    {
        var proc = await repo.GetByIdAsync(request.Id, ct);
        if (proc is null) return Result<ProcedureDto>.Failure("Không tìm thấy thủ thuật");
        return Result<ProcedureDto>.Success(ProcedureMapper.MapToDto(proc));
    }
}

// === Get by Patient ===

public record GetProceduresByPatientQuery(Guid PatientId) : IRequest<Result<IReadOnlyList<ProcedureDto>>>;

public class GetProceduresByPatientHandler(IProcedureRepository repo)
    : IRequestHandler<GetProceduresByPatientQuery, Result<IReadOnlyList<ProcedureDto>>>
{
    public async Task<Result<IReadOnlyList<ProcedureDto>>> Handle(GetProceduresByPatientQuery request, CancellationToken ct)
    {
        var items = await repo.GetByPatientAsync(request.PatientId, ct);
        return Result<IReadOnlyList<ProcedureDto>>.Success(items.Select(ProcedureMapper.MapToDto).ToList());
    }
}

// === Get by Cycle ===

public record GetProceduresByCycleQuery(Guid CycleId) : IRequest<Result<IReadOnlyList<ProcedureDto>>>;

public class GetProceduresByCycleHandler(IProcedureRepository repo)
    : IRequestHandler<GetProceduresByCycleQuery, Result<IReadOnlyList<ProcedureDto>>>
{
    public async Task<Result<IReadOnlyList<ProcedureDto>>> Handle(GetProceduresByCycleQuery request, CancellationToken ct)
    {
        var items = await repo.GetByCycleAsync(request.CycleId, ct);
        return Result<IReadOnlyList<ProcedureDto>>.Success(items.Select(ProcedureMapper.MapToDto).ToList());
    }
}

// === Get by Date ===

public record GetProceduresByDateQuery(DateTime Date) : IRequest<Result<IReadOnlyList<ProcedureDto>>>;

public class GetProceduresByDateHandler(IProcedureRepository repo)
    : IRequestHandler<GetProceduresByDateQuery, Result<IReadOnlyList<ProcedureDto>>>
{
    public async Task<Result<IReadOnlyList<ProcedureDto>>> Handle(GetProceduresByDateQuery request, CancellationToken ct)
    {
        var items = await repo.GetByDateAsync(request.Date, ct);
        return Result<IReadOnlyList<ProcedureDto>>.Success(items.Select(ProcedureMapper.MapToDto).ToList());
    }
}

// === Search ===

public record SearchProceduresQuery(
    string? Query = null,
    string? ProcedureType = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<ProcedureDto>>>;

public class SearchProceduresHandler(IProcedureRepository repo)
    : IRequestHandler<SearchProceduresQuery, Result<PagedResult<ProcedureDto>>>
{
    public async Task<Result<PagedResult<ProcedureDto>>> Handle(SearchProceduresQuery request, CancellationToken ct)
    {
        var items = await repo.SearchAsync(request.Query, request.ProcedureType, request.Status, request.Page, request.PageSize, ct);
        var total = await repo.CountAsync(request.Query, request.ProcedureType, request.Status, ct);
        var dtos = items.Select(ProcedureMapper.MapToDto).ToList();
        return Result<PagedResult<ProcedureDto>>.Success(new PagedResult<ProcedureDto>(dtos, total, request.Page, request.PageSize));
    }
}
