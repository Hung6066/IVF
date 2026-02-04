using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Users.Queries; 
using MediatR;

namespace IVF.Application.Features.Users.Queries;

// DTOs (if not using API Contracts)
public record UserDto(Guid Id, string Username, string FullName, string Role, string? Department, bool IsActive);

public record GetUsersQuery(string? Search, string? Role, bool? IsActive, int Page = 1, int PageSize = 20) : IRequest<UserListResponse>;

public record UserListResponse(List<UserDto> Items, int Total, int Page, int PageSize);

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, UserListResponse>
{
    private readonly IUserRepository _repo;

    public GetUsersQueryHandler(IUserRepository repo)
    {
        _repo = repo;
    }

    public async Task<UserListResponse> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _repo.SearchUsersAsync(request.Search, request.Role, request.IsActive, request.Page, request.PageSize, cancellationToken);
        var total = await _repo.CountUsersAsync(request.Search, request.Role, request.IsActive, cancellationToken);

        var dtos = users.Select(u => new UserDto(u.Id, u.Username, u.FullName, u.Role, u.Department, u.IsActive)).ToList();

        return new UserListResponse(dtos, total, request.Page, request.PageSize);
    }
}
