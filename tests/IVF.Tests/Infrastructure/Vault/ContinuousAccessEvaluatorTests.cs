using System.Text.Json;
using FluentAssertions;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace IVF.Tests.Infrastructure.Vault;

public class ContinuousAccessEvaluatorTests
{
    private readonly Mock<IVaultRepository> _repoMock;
    private readonly Mock<IZeroTrustService> _ztMock;
    private readonly Mock<ISecurityEventPublisher> _eventsMock;
    private readonly ContinuousAccessEvaluator _sut;

    public ContinuousAccessEvaluatorTests()
    {
        _repoMock = new Mock<IVaultRepository>();
        _ztMock = new Mock<IZeroTrustService>();
        _eventsMock = new Mock<ISecurityEventPublisher>();
        var loggerMock = new Mock<ILogger<ContinuousAccessEvaluator>>();

        // Default ZT service allows
        _ztMock.Setup(z => z.CheckVaultAccessAsync(It.IsAny<CheckZTAccessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ZTAccessDecision(true, ZTVaultAction.SecretRead, "OK", [], null, null, false, null, false, DateTime.UtcNow));

        _sut = new ContinuousAccessEvaluator(
            _repoMock.Object,
            _ztMock.Object,
            _eventsMock.Object,
            loggerMock.Object);
    }

    private static CaeSessionContext CreateSession(
        DateTime? startedAt = null,
        bool ipChanged = false,
        bool countryChanged = false)
    {
        return new CaeSessionContext(
            SessionId: "session-123",
            UserId: "user-1",
            DeviceId: "device-1",
            IpAddress: "192.168.1.1",
            Country: "VN",
            CurrentAuthLevel: AuthLevel.Session,
            SessionStartedAt: startedAt ?? DateTime.UtcNow.AddMinutes(-30),
            LastEvaluatedAt: DateTime.UtcNow.AddMinutes(-1),
            IpChanged: ipChanged,
            CountryChanged: countryChanged);
    }

    // ─── EvaluateSessionAsync Tests ──────────────────────

    [Fact]
    public async Task EvaluateSession_ValidSession_ContinuesAccess()
    {
        var session = CreateSession();

        var decision = await _sut.EvaluateSessionAsync(session);

        decision.ContinueAccess.Should().BeTrue();
        decision.RequiresReAuth.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateSession_ExpiredSession_DeniesAccess()
    {
        var session = CreateSession(startedAt: DateTime.UtcNow.AddHours(-9));

        var decision = await _sut.EvaluateSessionAsync(session);

        decision.ContinueAccess.Should().BeFalse();
        decision.RequiresReAuth.Should().BeTrue();
        decision.Reason.Should().Contain("maximum age");
    }

    [Fact]
    public async Task EvaluateSession_IpChanged_DeniesAccess()
    {
        var session = CreateSession(ipChanged: true);

        var decision = await _sut.EvaluateSessionAsync(session);

        decision.ContinueAccess.Should().BeFalse();
        decision.RequiresReAuth.Should().BeTrue();
        decision.Reason.Should().Contain("IP address changed");

        _eventsMock.Verify(e => e.PublishAsync(
            It.Is<VaultSecurityEvent>(v => v.EventType == "session.ip_changed"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateSession_CountryChanged_DeniesWithMfa()
    {
        var session = CreateSession(countryChanged: true);

        var decision = await _sut.EvaluateSessionAsync(session);

        decision.ContinueAccess.Should().BeFalse();
        decision.RequiredAuthLevel.Should().Be(AuthLevel.MFA);

        _eventsMock.Verify(e => e.PublishAsync(
            It.Is<VaultSecurityEvent>(v => v.EventType == "session.country_changed"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateSession_ZtDenied_DeniesAccess()
    {
        _ztMock.Setup(z => z.CheckVaultAccessAsync(It.IsAny<CheckZTAccessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ZTAccessDecision(false, ZTVaultAction.SecretRead, "Policy violation",
                ["test"], RiskLevel.High, 60m, true, AuthLevel.MFA, false, DateTime.UtcNow));

        var session = CreateSession();

        var decision = await _sut.EvaluateSessionAsync(session);

        decision.ContinueAccess.Should().BeFalse();
        decision.Reason.Should().Contain("Zero Trust re-evaluation failed");
    }

    // ─── CheckStepUpRequirementAsync Tests ──────────────

    [Fact]
    public async Task StepUp_CriticalAction_RequiresMfa()
    {
        var result = await _sut.CheckStepUpRequirementAsync(
            ZTVaultAction.SecretDelete, "user-1", AuthLevel.Session);

        result.Required.Should().BeTrue();
        result.RequiredLevel.Should().Be(AuthLevel.MFA);
        result.TimeoutSeconds.Should().Be(300);
    }

    [Fact]
    public async Task StepUp_ReadAction_NotRequired()
    {
        var result = await _sut.CheckStepUpRequirementAsync(
            ZTVaultAction.SecretRead, "user-1", AuthLevel.Session);

        result.Required.Should().BeFalse();
    }

    [Fact]
    public async Task StepUp_AlreadyMfa_NotRequired()
    {
        var result = await _sut.CheckStepUpRequirementAsync(
            ZTVaultAction.SecretDelete, "user-1", AuthLevel.MFA);

        result.Required.Should().BeFalse();
    }

    // ─── Session Binding Tests ──────────────────────────

    [Fact]
    public async Task BindSessionToken_SavesBinding()
    {
        var tokenId = Guid.NewGuid();

        await _sut.BindSessionTokenAsync("session-1", tokenId, "user-1");

        _repoMock.Verify(r => r.SaveSettingAsync(
            "session-binding-session-1",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeSessionTokens_WithBinding_RevokesToken()
    {
        var tokenId = Guid.NewGuid();
        var binding = JsonSerializer.Serialize(new
        {
            SessionId = "session-1",
            VaultTokenId = tokenId,
            UserId = "user-1",
            BoundAt = DateTime.UtcNow
        });
        var setting = VaultSetting.Create("session-binding-session-1", binding);

        _repoMock.Setup(r => r.GetSettingAsync("session-binding-session-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(setting);

        var revoked = await _sut.RevokeSessionTokensAsync("session-1", "suspicious activity");

        revoked.Should().Be(1);
        _repoMock.Verify(r => r.RevokeTokenAsync(tokenId, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.AddAuditLogAsync(It.IsAny<VaultAuditLog>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeSessionTokens_NoBinding_ReturnsZero()
    {
        _repoMock.Setup(r => r.GetSettingAsync("session-binding-none", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSetting?)null);

        var revoked = await _sut.RevokeSessionTokensAsync("none", "test");

        revoked.Should().Be(0);
    }
}
