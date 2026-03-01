using FluentAssertions;
using IVF.Application.Common.Behaviors;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;

namespace IVF.Tests.Infrastructure.Vault;

public class VaultPolicyBehaviorTests
{
    private readonly Mock<IVaultPolicyEvaluator> _evaluatorMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<ISecurityEventPublisher> _securityEventsMock;
    private readonly Mock<ILogger<VaultPolicyBehavior<TestPolicyRequest, string>>> _loggerMock;
    private readonly VaultPolicyBehavior<TestPolicyRequest, string> _sut;

    public VaultPolicyBehaviorTests()
    {
        _evaluatorMock = new Mock<IVaultPolicyEvaluator>();
        _currentUserMock = new Mock<ICurrentUserService>();
        _securityEventsMock = new Mock<ISecurityEventPublisher>();
        _loggerMock = new Mock<ILogger<VaultPolicyBehavior<TestPolicyRequest, string>>>();

        _sut = new VaultPolicyBehavior<TestPolicyRequest, string>(
            _evaluatorMock.Object,
            _currentUserMock.Object,
            _securityEventsMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithAllowedPolicy_ShouldCallNext()
    {
        // Arrange
        _evaluatorMock.Setup(e => e.EvaluateAsync("patients/records", "read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PolicyEvaluation(true, "doctor-read", "Granted"));

        var request = new TestPolicyRequest();
        var called = false;

        // Act
        var result = await _sut.Handle(request, _ =>
        {
            called = true;
            return Task.FromResult("success");
        }, CancellationToken.None);

        // Assert
        called.Should().BeTrue();
        result.Should().Be("success");
    }

    [Fact]
    public async Task Handle_WithDeniedPolicy_ShouldThrowUnauthorized()
    {
        // Arrange
        _evaluatorMock.Setup(e => e.EvaluateAsync("patients/records", "read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PolicyEvaluation(false, null, "No policy grants access"));
        _currentUserMock.Setup(u => u.Username).Returns("test-user");

        var request = new TestPolicyRequest();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.Handle(request, _ => Task.FromResult("should-not-reach"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithNonPolicyRequest_ShouldCallNextDirectly()
    {
        // Arrange — use a non-policy behavior with a plain request
        var evaluatorMock = new Mock<IVaultPolicyEvaluator>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var loggerMock = new Mock<ILogger<VaultPolicyBehavior<TestPlainRequest, string>>>();

        var behavior = new VaultPolicyBehavior<TestPlainRequest, string>(
            evaluatorMock.Object, currentUserMock.Object, new Mock<ISecurityEventPublisher>().Object, loggerMock.Object);

        var called = false;

        // Act
        var result = await behavior.Handle(new TestPlainRequest(), _ =>
        {
            called = true;
            return Task.FromResult("plain-result");
        }, CancellationToken.None);

        // Assert
        called.Should().BeTrue();
        result.Should().Be("plain-result");
        evaluatorMock.Verify(e => e.EvaluateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Test request implementing IVaultPolicyProtected
    public record TestPolicyRequest : IRequest<string>, IVaultPolicyProtected
    {
        public string ResourcePath => "patients/records";
        public string RequiredCapability => "read";
    }

    // Plain request (no vault policy)
    public record TestPlainRequest : IRequest<string>;
}

public class ZeroTrustBehaviorTests
{
    private readonly Mock<IZeroTrustService> _ztServiceMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<ISecurityEventPublisher> _securityEventsMock;
    private readonly Mock<ILogger<ZeroTrustBehavior<TestZtRequest, string>>> _loggerMock;
    private readonly ZeroTrustBehavior<TestZtRequest, string> _sut;

    public ZeroTrustBehaviorTests()
    {
        _ztServiceMock = new Mock<IZeroTrustService>();
        _currentUserMock = new Mock<ICurrentUserService>();
        _securityEventsMock = new Mock<ISecurityEventPublisher>();
        _loggerMock = new Mock<ILogger<ZeroTrustBehavior<TestZtRequest, string>>>();

        _sut = new ZeroTrustBehavior<TestZtRequest, string>(
            _ztServiceMock.Object,
            _currentUserMock.Object,
            _securityEventsMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithAllowedAccess_ShouldCallNext()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns(Guid.NewGuid());
        _currentUserMock.Setup(u => u.IpAddress).Returns("192.168.1.1");

        _ztServiceMock.Setup(z => z.CheckVaultAccessAsync(It.IsAny<CheckZTAccessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ZTAccessDecision(
                true, ZTVaultAction.SecretRead, "Access granted",
                new List<string>(), RiskLevel.Low, 0, false, null, false, DateTime.UtcNow));

        var called = false;
        var result = await _sut.Handle(new TestZtRequest(), _ =>
        {
            called = true;
            return Task.FromResult("ok");
        }, CancellationToken.None);

        called.Should().BeTrue();
        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_WithDeniedAccess_ShouldThrow()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns(Guid.NewGuid());
        _currentUserMock.Setup(u => u.IpAddress).Returns("192.168.1.1");

        _ztServiceMock.Setup(z => z.CheckVaultAccessAsync(It.IsAny<CheckZTAccessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ZTAccessDecision(
                false, ZTVaultAction.SecretRead, "Device risk too high",
                new List<string> { "High risk" }, RiskLevel.Critical, 85, false, null, false, DateTime.UtcNow));

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.Handle(new TestZtRequest(), _ => Task.FromResult("nope"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithNonZtRequest_ShouldCallNextDirectly()
    {
        var ztMock = new Mock<IZeroTrustService>();
        var cuMock = new Mock<ICurrentUserService>();
        var logMock = new Mock<ILogger<ZeroTrustBehavior<TestPlainZtRequest, string>>>();

        var behavior = new ZeroTrustBehavior<TestPlainZtRequest, string>(
            ztMock.Object, cuMock.Object, new Mock<ISecurityEventPublisher>().Object, logMock.Object);

        var called = false;
        var result = await behavior.Handle(new TestPlainZtRequest(), _ =>
        {
            called = true;
            return Task.FromResult("pass-through");
        }, CancellationToken.None);

        called.Should().BeTrue();
        result.Should().Be("pass-through");
        ztMock.Verify(z => z.CheckVaultAccessAsync(It.IsAny<CheckZTAccessRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public record TestZtRequest : IRequest<string>, IZeroTrustProtected
    {
        public ZTVaultAction RequiredAction => ZTVaultAction.SecretRead;
    }

    public record TestPlainZtRequest : IRequest<string>;
}
