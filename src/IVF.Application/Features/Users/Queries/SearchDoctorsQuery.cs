using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Users.Queries;

public record SearchDoctorsQuery(string? Search, int Page = 1, int PageSize = 20) : IRequest<List<DoctorDto>>;

public record DoctorDto(Guid Id, string FullName, string? Department, string? Phone);

public class SearchDoctorsQueryHandler : IRequestHandler<SearchDoctorsQuery, List<DoctorDto>>
{
    private readonly IUserRepository _repo;

    public SearchDoctorsQueryHandler(IUserRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<DoctorDto>> Handle(SearchDoctorsQuery request, CancellationToken cancellationToken)
    {
        var users = await _repo.GetUsersByRoleAsync("Doctor", request.Search, request.Page, request.PageSize, cancellationToken);
        return users.Select(u => new DoctorDto(u.Id, u.FullName, u.Department, null)).ToList(); // User entity might not have Phone exposed easily? I saw Patient has phone. User?
    }
}
