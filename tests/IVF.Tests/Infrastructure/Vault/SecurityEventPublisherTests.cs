using FluentAssertions;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Enums;
using IVF.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace IVF.Tests.Infrastructure.Vault;

public class SecurityEventPublisherTests
{
    private readonly SecurityEventPublisher _sut;
    private readonly Mock<ILogger<SecurityEventPublisher>> _loggerMock;

    public SecurityEventPublisherTests()
    {
        _loggerMock = new Mock<ILogger<SecurityEventPublisher>>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        _sut = new SecurityEventPublisher(_loggerMock.Object, config);
    }

    [Fact]
    public async Task PublishAsync_ShouldLogSecurityEvent()
    {
        var evt = new VaultSecurityEvent
        {
            EventType = "vault.policy.denied",
            Severity = SecuritySeverity.High,
            Source = "VaultPolicyBehavior",
            Action = "read",
            UserId = "user-123",
            IpAddress = "192.168.1.1",
            ResourceType = "VaultPolicy",
            ResourceId = "patients/records",
            Outcome = "deny",
            Reason = "No policy grants access"
        };

        await _sut.PublishAsync(evt);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, _) => o.ToString()!.Contains("SECURITY_EVENT")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_ShouldEmitCefLog()
    {
        var evt = new VaultSecurityEvent
        {
            EventType = "vault.zerotrust.denied",
            Severity = SecuritySeverity.Critical,
            Source = "ZeroTrustBehavior",
            Action = "SecretRead",
            Outcome = "deny",
            Reason = "Device risk"
        };

        await _sut.PublishAsync(evt);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, _) => o.ToString()!.Contains("CEF:")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldPublishAllEvents()
    {
        var events = new[]
        {
            new VaultSecurityEvent
            {
                EventType = "event1",
                Severity = SecuritySeverity.Low,
                Source = "test",
                Action = "test-action"
            },
            new VaultSecurityEvent
            {
                EventType = "event2",
                Severity = SecuritySeverity.Medium,
                Source = "test",
                Action = "test-action"
            }
        };

        await _sut.PublishBatchAsync(events);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(2));
    }

    [Fact]
    public void FormatCef_ShouldProduceValidCefString()
    {
        var evt = new VaultSecurityEvent
        {
            EventType = "vault.policy.denied",
            Severity = SecuritySeverity.High,
            Source = "VaultPolicyBehavior",
            Action = "read",
            UserId = "user-123",
            IpAddress = "10.0.0.1",
            ResourceType = "VaultPolicy",
            ResourceId = "secrets/config",
            Outcome = "deny",
            Reason = "No access"
        };

        var cef = SecurityEventPublisher.FormatCef(evt);

        cef.Should().StartWith("0|IVF|VaultSecurity|1.0|");
        cef.Should().Contain("vault.policy.denied");
        cef.Should().Contain("read");
        cef.Should().Contain("|3|"); // High severity = 3
        cef.Should().Contain("suid=user-123");
        cef.Should().Contain("src=10.0.0.1");
        cef.Should().Contain("cs1=VaultPolicy");
        cef.Should().Contain("outcome=deny");
    }

    [Fact]
    public void FormatCef_ShouldEscapeSpecialCharacters()
    {
        var evt = new VaultSecurityEvent
        {
            EventType = "test|event",
            Severity = SecuritySeverity.Info,
            Source = "test",
            Action = "act=ion",
            UserId = "user\\1"
        };

        var cef = SecurityEventPublisher.FormatCef(evt);

        cef.Should().Contain("test\\|event");
        cef.Should().Contain("act\\=ion");
        cef.Should().Contain("suid=user\\\\1");
    }

    [Fact]
    public void FormatCef_WithExtensions_ShouldIncludeThem()
    {
        var evt = new VaultSecurityEvent
        {
            EventType = "test",
            Severity = SecuritySeverity.Low,
            Source = "test",
            Action = "check",
            Extensions = new Dictionary<string, string>
            {
                ["customField"] = "customValue"
            }
        };

        var cef = SecurityEventPublisher.FormatCef(evt);

        cef.Should().Contain("customField=customValue");
    }
}
