using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Users.Queries;

public record SearchDoctorsQuery(string? Search, int Page = 1, int PageSize = 20) : IRequest<List<DoctorDto>>;

public record DoctorDto(Guid Id, string FullName, string? Department, string? Phone);

public class SearchDoctorsQueryHandler : IRequestHandler<SearchDoctorsQuery, List<DoctorDto>>
{
    private readonly IDoctorRepository _repo;

    public SearchDoctorsQueryHandler(IDoctorRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<DoctorDto>> Handle(SearchDoctorsQuery request, CancellationToken cancellationToken)
    {
        var doctors = await _repo.SearchAsync(request.Search, request.Page, request.PageSize, cancellationToken);
        return doctors.Select(d => new DoctorDto(d.Id, d.User.FullName, d.Specialty, null)).ToList();
    }
}
