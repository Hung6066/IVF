using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Users.Commands;

// ═══════════════════════════════════════════════════════════════
// SESSION HANDLERS
// ═══════════════════════════════════════════════════════════════

public class CreateUserSessionHandler(IEnterpriseUserRepository repo) : IRequestHandler<CreateUserSessionCommand, Guid>
{
    public async Task<Guid> Handle(CreateUserSessionCommand r, CancellationToken ct)
    {
        var session = UserSession.Create(
            r.UserId, r.SessionToken, r.ExpiresAt,
            r.IpAddress, r.UserAgent, r.DeviceFingerprint,
            r.Country, r.City, r.DeviceType, r.OperatingSystem, r.Browser);

        await repo.AddSessionAsync(session, ct);
        await repo.SaveChangesAsync(ct);
        return session.Id;
    }
}

public class RevokeUserSessionHandler(IEnterpriseUserRepository repo) : IRequestHandler<RevokeUserSessionCommand>
{
    public async Task Handle(RevokeUserSessionCommand r, CancellationToken ct)
    {
        var session = await repo.GetSessionByIdAsync(r.SessionId, ct);
        if (session != null)
        {
            session.Revoke(r.Reason, r.RevokedBy);
            await repo.SaveChangesAsync(ct);
        }
    }
}

public class RevokeAllUserSessionsHandler(IEnterpriseUserRepository repo) : IRequestHandler<RevokeAllUserSessionsCommand, int>
{
    public async Task<int> Handle(RevokeAllUserSessionsCommand r, CancellationToken ct)
    {
        return await repo.RevokeAllSessionsAsync(r.UserId, r.Reason, r.RevokedBy, ct);
    }
}

// ═══════════════════════════════════════════════════════════════
// GROUP HANDLERS
// ═══════════════════════════════════════════════════════════════

public class CreateUserGroupHandler(IEnterpriseUserRepository repo) : IRequestHandler<CreateUserGroupCommand, Guid>
{
    public async Task<Guid> Handle(CreateUserGroupCommand r, CancellationToken ct)
    {
        var group = UserGroup.Create(r.Name, r.DisplayName, r.Description, r.GroupType, r.ParentGroupId);
        await repo.AddGroupAsync(group, ct);
        await repo.SaveChangesAsync(ct);
        return group.Id;
    }
}

public class UpdateUserGroupHandler(IEnterpriseUserRepository repo) : IRequestHandler<UpdateUserGroupCommand>
{
    public async Task Handle(UpdateUserGroupCommand r, CancellationToken ct)
    {
        var group = await repo.GetGroupByIdAsync(r.Id, ct)
            ?? throw new KeyNotFoundException($"Group {r.Id} not found");
        group.Update(r.Name, r.DisplayName, r.Description, r.GroupType);
        await repo.SaveChangesAsync(ct);
    }
}

public class DeleteUserGroupHandler(IEnterpriseUserRepository repo) : IRequestHandler<DeleteUserGroupCommand>
{
    public async Task Handle(DeleteUserGroupCommand r, CancellationToken ct)
    {
        var group = await repo.GetGroupByIdAsync(r.Id, ct);
        if (group != null)
        {
            if (group.IsSystem) throw new InvalidOperationException("Cannot delete system group");
            group.MarkAsDeleted();
            await repo.SaveChangesAsync(ct);
        }
    }
}

public class AddGroupMemberHandler(IEnterpriseUserRepository repo) : IRequestHandler<AddGroupMemberCommand, Guid>
{
    public async Task<Guid> Handle(AddGroupMemberCommand r, CancellationToken ct)
    {
        var existing = await repo.GetGroupMemberAsync(r.GroupId, r.UserId, ct);
        if (existing != null) throw new InvalidOperationException("User is already a member of this group");

        var member = UserGroupMember.Create(r.UserId, r.GroupId, r.MemberRole, r.AddedBy);
        await repo.AddGroupMemberAsync(member, ct);
        await repo.SaveChangesAsync(ct);
        return member.Id;
    }
}

public class RemoveGroupMemberHandler(IEnterpriseUserRepository repo) : IRequestHandler<RemoveGroupMemberCommand>
{
    public async Task Handle(RemoveGroupMemberCommand r, CancellationToken ct)
    {
        var member = await repo.GetGroupMemberAsync(r.GroupId, r.UserId, ct);
        if (member != null)
        {
            member.MarkAsDeleted();
            await repo.SaveChangesAsync(ct);
        }
    }
}

public class UpdateGroupMemberRoleHandler(IEnterpriseUserRepository repo) : IRequestHandler<UpdateGroupMemberRoleCommand>
{
    public async Task Handle(UpdateGroupMemberRoleCommand r, CancellationToken ct)
    {
        var member = await repo.GetGroupMemberAsync(r.GroupId, r.UserId, ct)
            ?? throw new KeyNotFoundException("Member not found in group");
        member.UpdateRole(r.MemberRole);
        await repo.SaveChangesAsync(ct);
    }
}

public class AssignGroupPermissionsHandler(IEnterpriseUserRepository repo) : IRequestHandler<AssignGroupPermissionsCommand>
{
    public async Task Handle(AssignGroupPermissionsCommand r, CancellationToken ct)
    {
        var newPermissions = r.Permissions
            .Select(permCode => UserGroupPermission.Create(r.GroupId, permCode, r.GrantedBy))
            .ToList();
        await repo.ReplaceGroupPermissionsAsync(r.GroupId, newPermissions, ct);
        await repo.SaveChangesAsync(ct);
    }
}

// ═══════════════════════════════════════════════════════════════
// LOGIN HISTORY HANDLERS
// ═══════════════════════════════════════════════════════════════

public class RecordLoginHistoryHandler(IEnterpriseUserRepository repo) : IRequestHandler<RecordLoginHistoryCommand, Guid>
{
    public async Task<Guid> Handle(RecordLoginHistoryCommand r, CancellationToken ct)
    {
        var history = UserLoginHistory.Create(
            r.UserId, r.LoginMethod, r.IsSuccess, r.FailureReason,
            r.IpAddress, r.UserAgent, r.DeviceFingerprint,
            r.Country, r.City, r.DeviceType, r.OperatingSystem, r.Browser,
            r.RiskScore, r.IsSuspicious, r.RiskFactors);

        await repo.AddLoginHistoryAsync(history, ct);
        await repo.SaveChangesAsync(ct);
        return history.Id;
    }
}

public class RecordLogoutHandler(IEnterpriseUserRepository repo) : IRequestHandler<RecordLogoutCommand>
{
    public async Task Handle(RecordLogoutCommand r, CancellationToken ct)
    {
        var history = await repo.GetLoginHistoryByIdAsync(r.LoginHistoryId, ct);
        if (history != null)
        {
            history.RecordLogout();
            await repo.SaveChangesAsync(ct);
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// CONSENT HANDLERS
// ═══════════════════════════════════════════════════════════════

public class GrantConsentHandler(IEnterpriseUserRepository repo) : IRequestHandler<GrantConsentCommand, Guid>
{
    public async Task<Guid> Handle(GrantConsentCommand r, CancellationToken ct)
    {
        await repo.SupersedeConsentsAsync(r.UserId, r.ConsentType, ct);

        var consent = UserConsent.Grant(r.UserId, r.ConsentType, r.ConsentVersion, r.IpAddress, r.UserAgent, r.ExpiresAt);
        await repo.AddConsentAsync(consent, ct);
        await repo.SaveChangesAsync(ct);
        return consent.Id;
    }
}

public class RevokeConsentHandler(IEnterpriseUserRepository repo) : IRequestHandler<RevokeConsentCommand>
{
    public async Task Handle(RevokeConsentCommand r, CancellationToken ct)
    {
        var consent = await repo.GetConsentByIdAsync(r.ConsentId, ct);
        if (consent != null)
        {
            consent.Revoke(r.Reason);
            await repo.SaveChangesAsync(ct);
        }
    }
}
