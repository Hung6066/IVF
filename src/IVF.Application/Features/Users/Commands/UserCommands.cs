using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;
using FluentValidation;

namespace IVF.Application.Features.Users.Commands;

// Create
public record CreateUserCommand(string Username, string Password, string FullName, string Role, string? Department) : IRequest<Guid>;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Guid>
{
    private readonly IUserRepository _repo;
    private readonly IUnitOfWork _uow;

    public CreateUserCommandHandler(IUserRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<Guid> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repo.GetByUsernameAsync(request.Username, cancellationToken);
        if (existing != null) throw new ValidationException("Username already exists");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var user = User.Create(request.Username, passwordHash, request.FullName, request.Role, request.Department);

        await _repo.AddAsync(user, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}

// Update
public record UpdateUserCommand(Guid Id, string FullName, string Role, string? Department, bool IsActive, string? Password = null) : IRequest;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand>
{
    private readonly IUserRepository _repo;
    private readonly IUnitOfWork _uow;

    public UpdateUserCommandHandler(IUserRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _repo.GetByIdAsync(request.Id, cancellationToken);
        if (user == null) throw new KeyNotFoundException($"User {request.Id} not found");

        user.UpdateInfo(request.FullName, request.Role, request.Department);
        
        if (request.IsActive && !user.IsActive) user.Activate();
        if (!request.IsActive && user.IsActive) user.Deactivate();

        if (!string.IsNullOrEmpty(request.Password))
        {
            user.UpdatePassword(BCrypt.Net.BCrypt.HashPassword(request.Password));
        }

        await _repo.UpdateAsync(user, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
    }
}

// Delete (Deactivate)
public record DeleteUserCommand(Guid Id) : IRequest;

public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand>
{
    private readonly IUserRepository _repo;
    private readonly IUnitOfWork _uow;

    public DeleteUserCommandHandler(IUserRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _repo.GetByIdAsync(request.Id, cancellationToken);
        if (user != null)
        {
            user.Deactivate(); // Soft delete preferred
            await _repo.UpdateAsync(user, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
        }
    }
}
