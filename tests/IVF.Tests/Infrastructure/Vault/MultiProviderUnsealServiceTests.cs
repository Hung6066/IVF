using System.Text.Json;
using FluentAssertions;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace IVF.Tests.Infrastructure.Vault;

public class MultiProviderUnsealServiceTests
{
    private readonly Mock<IKeyVaultService> _kvMock;
    private readonly Mock<IVaultRepository> _repoMock;
    private readonly Mock<ISecurityEventPublisher> _eventsMock;
    private readonly MultiProviderUnsealService _sut;

    public MultiProviderUnsealServiceTests()
    {
        _kvMock = new Mock<IKeyVaultService>();
        _repoMock = new Mock<IVaultRepository>();
        _eventsMock = new Mock<ISecurityEventPublisher>();
        var loggerMock = new Mock<ILogger<MultiProviderUnsealService>>();

        _kvMock.Setup(k => k.AutoUnsealAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _kvMock.Setup(k => k.IsHealthyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _kvMock.Setup(k => k.ConfigureAutoUnsealAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Default: no providers configured
        _repoMock.Setup(r => r.GetSettingAsync("unseal-providers", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSetting?)null);

        _sut = new MultiProviderUnsealService(
            _kvMock.Object, _repoMock.Object, _eventsMock.Object, loggerMock.Object);
    }

    private void SetupProviders(params (string id, string type, int priority)[] providers)
    {
        var stored = providers.Select(p => new
        {
            ProviderId = p.id,
            ProviderType = p.type,
            Priority = p.priority,
            KeyIdentifier = "key-1",
            Settings = new Dictionary<string, string>(),
            ConfiguredAt = DateTime.UtcNow,
            LastUsedAt = (DateTime?)null,
        }).ToList();

        var json = JsonSerializer.Serialize(stored);
        _repoMock.Setup(r => r.GetSettingAsync("unseal-providers", It.IsAny<CancellationToken>()))
            .ReturnsAsync(VaultSetting.Create("unseal-providers", json));
    }

    // ─── AutoUnseal Tests ───────────────────────────────

    [Fact]
    public async Task AutoUnseal_NoProviders_FallsBackToDefault()
    {
        var result = await _sut.AutoUnsealAsync();

        result.Success.Should().BeTrue();
        result.ProviderId.Should().Be("default-azure");
        result.AttemptsTotal.Should().Be(1);
    }

    [Fact]
    public async Task AutoUnseal_PrimarySucceeds_ReturnsFirst()
    {
        SetupProviders(("azure-primary", "Azure", 1), ("azure-secondary", "Azure", 2));

        var result = await _sut.AutoUnsealAsync();

        result.Success.Should().BeTrue();
        result.ProviderId.Should().Be("azure-primary");
        result.AttemptsTotal.Should().Be(1);
    }

    [Fact]
    public async Task AutoUnseal_PrimaryFails_FallsBackToSecondary()
    {
        SetupProviders(("primary", "Azure", 1), ("secondary", "Local", 2));

        var callCount = 0;
        _kvMock.Setup(k => k.AutoUnsealAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount > 1; // First call fails, second succeeds
            });

        var result = await _sut.AutoUnsealAsync();

        result.Success.Should().BeTrue();
        result.ProviderId.Should().Be("secondary");
        result.AttemptsTotal.Should().Be(2);
    }

    [Fact]
    public async Task AutoUnseal_AllFail_PublishesCriticalEvent()
    {
        SetupProviders(("p1", "Azure", 1));
        _kvMock.Setup(k => k.AutoUnsealAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _sut.AutoUnsealAsync();

        result.Success.Should().BeFalse();
        _eventsMock.Verify(e => e.PublishAsync(
            It.Is<VaultSecurityEvent>(v => v.EventType == "vault.unseal.all_failed"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── Configure Tests ────────────────────────────────

    [Fact]
    public async Task Configure_AzureProvider_DelegatestoKvService()
    {
        var config = new UnsealProviderConfig("azure-1", "Azure", 1, "my-key");

        var result = await _sut.ConfigureProviderAsync(config, "master-password");

        result.Should().BeTrue();
        _kvMock.Verify(k => k.ConfigureAutoUnsealAsync("master-password", "my-key", It.IsAny<CancellationToken>()));
        _repoMock.Verify(r => r.AddAuditLogAsync(It.IsAny<VaultAuditLog>(), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Configure_SavesProviderToSettings()
    {
        var config = new UnsealProviderConfig("local-1", "Local", 2, "local-key");

        await _sut.ConfigureProviderAsync(config, "password");

        _repoMock.Verify(r => r.SaveSettingAsync(
            "unseal-providers", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── Provider Status Tests ──────────────────────────

    [Fact]
    public async Task GetProviderStatus_ReturnsOrderedStatuses()
    {
        SetupProviders(("secondary", "Local", 2), ("primary", "Azure", 1));

        var statuses = await _sut.GetProviderStatusAsync();

        statuses.Should().HaveCount(2);
        statuses[0].ProviderId.Should().Be("primary");
        statuses[0].Available.Should().BeTrue();
        statuses[1].ProviderId.Should().Be("secondary");
    }

    [Fact]
    public async Task GetProviderStatus_UnavailableAzure_ReportsError()
    {
        SetupProviders(("azure-1", "Azure", 1));
        _kvMock.Setup(k => k.IsHealthyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var statuses = await _sut.GetProviderStatusAsync();

        statuses[0].Available.Should().BeFalse();
        statuses[0].Error.Should().Be("Provider unavailable");
    }
}
