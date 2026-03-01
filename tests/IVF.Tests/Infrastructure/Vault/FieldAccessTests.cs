using FluentAssertions;
using IVF.Application.Common.Behaviors;
using IVF.Application.Common.Interfaces;
using IVF.Application.Common.Services;
using IVF.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;

namespace IVF.Tests.Infrastructure.Vault;

public class FieldAccessServiceTests
{
    private readonly Mock<IVaultRepository> _repoMock;
    private readonly FieldAccessService _sut;

    public FieldAccessServiceTests()
    {
        _repoMock = new Mock<IVaultRepository>();
        var loggerMock = new Mock<ILogger<FieldAccessService>>();
        _sut = new FieldAccessService(_repoMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task ApplyFieldAccess_AdminRole_SkipsProcessing()
    {
        var dto = new TestDto { Name = "Secret", Email = "admin@test.com" };
        await _sut.ApplyFieldAccessAsync(dto, "patients", "Admin", CancellationToken.None);

        dto.Name.Should().Be("Secret");
        dto.Email.Should().Be("admin@test.com");
        _repoMock.Verify(r => r.GetAllFieldAccessPoliciesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyFieldAccess_MaskedLevel_ReplacesWithPattern()
    {
        var policy = FieldAccessPolicy.Create("patients", "Email", "Nurse", "masked", "***HIDDEN***");
        _repoMock.Setup(r => r.GetAllFieldAccessPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FieldAccessPolicy> { policy });

        var dto = new TestDto { Name = "John", Email = "john@test.com" };
        await _sut.ApplyFieldAccessAsync(dto, "patients", "Nurse", CancellationToken.None);

        dto.Email.Should().Be("***HIDDEN***");
        dto.Name.Should().Be("John"); // No policy for Name
    }

    [Fact]
    public async Task ApplyFieldAccess_PartialLevel_TruncatesAndMasks()
    {
        var policy = FieldAccessPolicy.Create("patients", "Email", "LabTech", "partial", "...", 3);
        _repoMock.Setup(r => r.GetAllFieldAccessPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FieldAccessPolicy> { policy });

        var dto = new TestDto { Name = "John", Email = "john@test.com" };
        await _sut.ApplyFieldAccessAsync(dto, "patients", "LabTech", CancellationToken.None);

        dto.Email.Should().Be("joh...");
    }

    [Fact]
    public async Task ApplyFieldAccess_NoneLevel_SetsToNull()
    {
        var policy = FieldAccessPolicy.Create("patients", "Email", "Receptionist", "none");
        _repoMock.Setup(r => r.GetAllFieldAccessPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FieldAccessPolicy> { policy });

        var dto = new TestDto { Name = "John", Email = "john@test.com" };
        await _sut.ApplyFieldAccessAsync(dto, "patients", "Receptionist", CancellationToken.None);

        dto.Email.Should().BeNull();
    }

    [Fact]
    public async Task ApplyFieldAccess_FullLevel_LeavesUnchanged()
    {
        var policy = FieldAccessPolicy.Create("patients", "Email", "Doctor", "full");
        _repoMock.Setup(r => r.GetAllFieldAccessPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FieldAccessPolicy> { policy });

        var dto = new TestDto { Name = "John", Email = "john@test.com" };
        await _sut.ApplyFieldAccessAsync(dto, "patients", "Doctor", CancellationToken.None);

        dto.Email.Should().Be("john@test.com");
    }

    [Fact]
    public async Task ApplyFieldAccess_NoPoliciesForRole_LeavesUnchanged()
    {
        _repoMock.Setup(r => r.GetAllFieldAccessPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FieldAccessPolicy>());

        var dto = new TestDto { Name = "John", Email = "john@test.com" };
        await _sut.ApplyFieldAccessAsync(dto, "patients", "Nurse", CancellationToken.None);

        dto.Email.Should().Be("john@test.com");
    }

    [Fact]
    public async Task ApplyFieldAccess_Collection_MasksAllItems()
    {
        var policy = FieldAccessPolicy.Create("patients", "Email", "Nurse", "masked");
        _repoMock.Setup(r => r.GetAllFieldAccessPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FieldAccessPolicy> { policy });

        var dtos = new List<TestDto>
        {
            new() { Name = "A", Email = "a@test.com" },
            new() { Name = "B", Email = "b@test.com" },
        };

        await _sut.ApplyFieldAccessAsync<TestDto>(dtos, "patients", "Nurse", CancellationToken.None);

        dtos.Should().AllSatisfy(d => d.Email.Should().Be("********"));
    }

    [Fact]
    public async Task ApplyFieldAccess_EmptyStringField_Skipped()
    {
        var policy = FieldAccessPolicy.Create("patients", "Email", "Nurse", "masked");
        _repoMock.Setup(r => r.GetAllFieldAccessPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FieldAccessPolicy> { policy });

        var dto = new TestDto { Name = "John", Email = "" };
        await _sut.ApplyFieldAccessAsync(dto, "patients", "Nurse", CancellationToken.None);

        dto.Email.Should().BeEmpty(); // Empty string skipped
    }

    [Fact]
    public async Task ApplyFieldAccess_CaseInsensitiveTableAndRole()
    {
        var policy = FieldAccessPolicy.Create("Patients", "Email", "nurse", "masked");
        _repoMock.Setup(r => r.GetAllFieldAccessPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FieldAccessPolicy> { policy });

        var dto = new TestDto { Name = "John", Email = "john@test.com" };
        await _sut.ApplyFieldAccessAsync(dto, "patients", "Nurse", CancellationToken.None);

        dto.Email.Should().Be("********");
    }

    [Fact]
    public async Task ApplyFieldAccess_CachesPoliciesAcrossCalls()
    {
        var policy = FieldAccessPolicy.Create("patients", "Email", "Nurse", "masked");
        _repoMock.Setup(r => r.GetAllFieldAccessPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FieldAccessPolicy> { policy });

        var dto1 = new TestDto { Name = "A", Email = "a@test.com" };
        var dto2 = new TestDto { Name = "B", Email = "b@test.com" };

        await _sut.ApplyFieldAccessAsync(dto1, "patients", "Nurse", CancellationToken.None);
        await _sut.ApplyFieldAccessAsync(dto2, "patients", "Nurse", CancellationToken.None);

        _repoMock.Verify(r => r.GetAllFieldAccessPoliciesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    public class TestDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
    }
}

public class FieldAccessBehaviorTests
{
    [Fact]
    public async Task Handle_NonProtectedRequest_ReturnsResponseUnchanged()
    {
        var fieldSvcMock = new Mock<IFieldAccessService>();
        var userMock = new Mock<ICurrentUserService>();
        var logMock = new Mock<ILogger<FieldAccessBehavior<PlainFieldRequest, string>>>();

        var behavior = new FieldAccessBehavior<PlainFieldRequest, string>(
            fieldSvcMock.Object, userMock.Object, logMock.Object);

        var result = await behavior.Handle(new PlainFieldRequest(), _ => Task.FromResult("data"), CancellationToken.None);

        result.Should().Be("data");
        fieldSvcMock.Verify(s => s.ApplyFieldAccessAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AdminRole_SkipsFieldAccess()
    {
        var fieldSvcMock = new Mock<IFieldAccessService>();
        var userMock = new Mock<ICurrentUserService>();
        userMock.Setup(u => u.Role).Returns("Admin");
        var logMock = new Mock<ILogger<FieldAccessBehavior<ProtectedFieldRequest, string>>>();

        var behavior = new FieldAccessBehavior<ProtectedFieldRequest, string>(
            fieldSvcMock.Object, userMock.Object, logMock.Object);

        var result = await behavior.Handle(new ProtectedFieldRequest(), _ => Task.FromResult("data"), CancellationToken.None);

        result.Should().Be("data");
    }

    [Fact]
    public async Task Handle_NullResponse_ReturnsNull()
    {
        var fieldSvcMock = new Mock<IFieldAccessService>();
        var userMock = new Mock<ICurrentUserService>();
        userMock.Setup(u => u.Role).Returns("Nurse");
        var logMock = new Mock<ILogger<FieldAccessBehavior<ProtectedFieldRequest, string?>>>();

        var behavior = new FieldAccessBehavior<ProtectedFieldRequest, string?>(
            fieldSvcMock.Object, userMock.Object, logMock.Object);

        var result = await behavior.Handle(new ProtectedFieldRequest(), _ => Task.FromResult<string?>(null), CancellationToken.None);

        result.Should().BeNull();
    }

    public record PlainFieldRequest : IRequest<string>;

    public record ProtectedFieldRequest : IRequest<string>, IFieldAccessProtected
    {
        public string TableName => "patients";
    }
}
