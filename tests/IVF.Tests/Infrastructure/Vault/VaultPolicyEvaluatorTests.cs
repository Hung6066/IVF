using FluentAssertions;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics.Metrics;
using System.Security.Claims;

namespace IVF.Tests.Infrastructure.Vault;

public class VaultPolicyEvaluatorTests
{
    private readonly Mock<IVaultRepository> _repoMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<IHttpContextAccessor> _httpContextMock;
    private readonly Mock<ILogger<VaultPolicyEvaluator>> _loggerMock;
    private readonly VaultPolicyEvaluator _sut;

    public VaultPolicyEvaluatorTests()
    {
        _repoMock = new Mock<IVaultRepository>();
        _currentUserMock = new Mock<ICurrentUserService>();
        _httpContextMock = new Mock<IHttpContextAccessor>();
        _loggerMock = new Mock<ILogger<VaultPolicyEvaluator>>();
        var metrics = CreateVaultMetrics();

        _sut = new VaultPolicyEvaluator(
            _repoMock.Object,
            _currentUserMock.Object,
            _httpContextMock.Object,
            _loggerMock.Object,
            metrics);
    }

    private static VaultMetrics CreateVaultMetrics()
    {
        var meterFactoryMock = new Mock<IMeterFactory>();
        meterFactoryMock.Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns((MeterOptions opts) => new Meter(opts));
        return new VaultMetrics(meterFactoryMock.Object);
    }

    private void SetupHttpContext(ClaimsPrincipal? principal = null)
    {
        var httpContext = new DefaultHttpContext();
        if (principal is not null)
            httpContext.User = principal;
        _httpContextMock.Setup(h => h.HttpContext).Returns(httpContext);
    }

    [Fact]
    public async Task EvaluateAsync_AdminRole_ShouldBypass()
    {
        // Arrange
        _currentUserMock.Setup(u => u.Role).Returns("Admin");
        SetupHttpContext();

        // Act
        var result = await _sut.EvaluateAsync("any/path", "delete");

        // Assert
        result.Allowed.Should().BeTrue();
        result.MatchedPolicy.Should().Be("admin-bypass");
    }

    [Fact]
    public async Task EvaluateAsync_AdminRole_CaseInsensitive()
    {
        _currentUserMock.Setup(u => u.Role).Returns("admin");
        SetupHttpContext();

        var result = await _sut.EvaluateAsync("any/path", "sudo");

        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_UnauthenticatedUser_ShouldDeny()
    {
        _currentUserMock.Setup(u => u.Role).Returns("Doctor");
        _currentUserMock.Setup(u => u.UserId).Returns((Guid?)null);
        SetupHttpContext();

        var result = await _sut.EvaluateAsync("secrets/data", "read");

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Contain("not authenticated");
    }

    [Fact]
    public async Task EvaluateAsync_UserWithMatchingPolicy_ShouldAllow()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserMock.Setup(u => u.Role).Returns("Doctor");
        _currentUserMock.Setup(u => u.UserId).Returns(userId);
        SetupHttpContext();

        var policy = VaultPolicy.Create("doctor-read", "patients/*", ["read", "list"]);
        var userPolicy = CreateUserPolicy(userId, policy);

        _repoMock.Setup(r => r.GetUserPoliciesByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultUserPolicy> { userPolicy });
        _repoMock.Setup(r => r.GetPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultPolicy> { policy });

        // Act
        var result = await _sut.EvaluateAsync("patients/records", "read");

        // Assert
        result.Allowed.Should().BeTrue();
        result.MatchedPolicy.Should().Be("doctor-read");
    }

    [Fact]
    public async Task EvaluateAsync_UserWithNoMatchingCapability_ShouldDeny()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserMock.Setup(u => u.Role).Returns("Nurse");
        _currentUserMock.Setup(u => u.UserId).Returns(userId);
        SetupHttpContext();

        var policy = VaultPolicy.Create("nurse-read", "patients/*", ["read"]);
        var userPolicy = CreateUserPolicy(userId, policy);

        _repoMock.Setup(r => r.GetUserPoliciesByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultUserPolicy> { userPolicy });
        _repoMock.Setup(r => r.GetPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultPolicy> { policy });

        // Act — requesting "delete" but only "read" granted
        var result = await _sut.EvaluateAsync("patients/records", "delete");

        // Assert
        result.Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_UserWithNoPolicies_ShouldDeny()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserMock.Setup(u => u.Role).Returns("Receptionist");
        _currentUserMock.Setup(u => u.UserId).Returns(userId);
        SetupHttpContext();

        _repoMock.Setup(r => r.GetUserPoliciesByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultUserPolicy>());

        // Act
        var result = await _sut.EvaluateAsync("secrets/data", "read");

        // Assert
        result.Allowed.Should().BeFalse();
        result.Reason.Should().Contain("No vault policies");
    }

    [Fact]
    public async Task EvaluateAsync_VaultTokenAuth_WithMatchingPolicy_ShouldAllow()
    {
        // Arrange
        _currentUserMock.Setup(u => u.Role).Returns((string?)null);

        var claims = new[]
        {
            new Claim("auth_method", "vault_token"),
            new Claim(ClaimTypes.Role, "secrets-read"),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "vault"));
        SetupHttpContext(principal);

        var policy = VaultPolicy.Create("secrets-read", "secrets/**", ["read"]);
        _repoMock.Setup(r => r.GetPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultPolicy> { policy });

        // Act
        var result = await _sut.EvaluateAsync("secrets/config/db", "read");

        // Assert
        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_VaultTokenAuth_WithNoPolicies_ShouldDeny()
    {
        // Arrange
        _currentUserMock.Setup(u => u.Role).Returns((string?)null);

        var claims = new[]
        {
            new Claim("auth_method", "vault_token"),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "vault"));
        SetupHttpContext(principal);

        // Act
        var result = await _sut.EvaluateAsync("secrets/data", "read");

        // Assert
        result.Allowed.Should().BeFalse();
        result.Reason.Should().Contain("no policies");
    }

    [Fact]
    public async Task EvaluateAsync_SudoPolicyShouldGrantAllCapabilities()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserMock.Setup(u => u.Role).Returns("Doctor");
        _currentUserMock.Setup(u => u.UserId).Returns(userId);
        SetupHttpContext();

        var policy = VaultPolicy.Create("super-admin", "**", ["sudo"]);
        var userPolicy = CreateUserPolicy(userId, policy);

        _repoMock.Setup(r => r.GetUserPoliciesByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultUserPolicy> { userPolicy });
        _repoMock.Setup(r => r.GetPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultPolicy> { policy });

        // Act
        var result = await _sut.EvaluateAsync("any/deep/path", "delete");

        // Assert
        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateForUserAsync_ShouldEvaluateForSpecificUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var policy = VaultPolicy.Create("data-reader", "data/*", ["read"]);
        var userPolicy = CreateUserPolicy(userId, policy);

        _repoMock.Setup(r => r.GetUserPoliciesByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultUserPolicy> { userPolicy });
        _repoMock.Setup(r => r.GetPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultPolicy> { policy });

        // Act
        var result = await _sut.EvaluateForUserAsync(userId, "data/patients", "read");

        // Assert
        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task GetEffectivePoliciesAsync_ForAdmin_ShouldReturnFullAccess()
    {
        // Arrange
        _currentUserMock.Setup(u => u.Role).Returns("Admin");
        SetupHttpContext();

        // Act
        var result = await _sut.GetEffectivePoliciesAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].PolicyName.Should().Be("admin");
        result[0].PathPattern.Should().Be("**");
        result[0].Capabilities.Should().Contain("sudo");
    }

    [Fact]
    public async Task GetEffectivePoliciesForUserAsync_ShouldReturnAssignedPolicies()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var policy1 = VaultPolicy.Create("p1", "a/*", ["read"]);
        var policy2 = VaultPolicy.Create("p2", "b/*", ["read", "create"]);

        _repoMock.Setup(r => r.GetUserPoliciesByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultUserPolicy>
            {
                CreateUserPolicy(userId, policy1),
                CreateUserPolicy(userId, policy2),
            });
        _repoMock.Setup(r => r.GetPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultPolicy> { policy1, policy2 });

        // Act
        var result = await _sut.GetEffectivePoliciesForUserAsync(userId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(p => p.PolicyName == "p1");
        result.Should().Contain(p => p.PolicyName == "p2");
    }

    // Helper to create VaultUserPolicy via reflection (private constructor)
    private static VaultUserPolicy CreateUserPolicy(Guid userId, VaultPolicy policy)
    {
        var type = typeof(VaultUserPolicy);
        var instance = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type) as VaultUserPolicy;

        // Set properties via reflection
        type.GetProperty("UserId")!.SetValue(instance, userId);
        type.GetProperty("PolicyId")!.SetValue(instance, policy.Id);
        type.GetProperty("Policy")!.SetValue(instance, policy);

        return instance!;
    }
}
