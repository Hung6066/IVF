using FluentAssertions;
using IVF.Domain.Entities;

namespace IVF.Tests.Domain;

public class VaultSecretTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var secret = VaultSecret.Create("config/db/host", "encrypted", "iv123", Guid.NewGuid(), "{}", 1);

        secret.Path.Should().Be("config/db/host");
        secret.EncryptedData.Should().Be("encrypted");
        secret.Iv.Should().Be("iv123");
        secret.Version.Should().Be(1);
        secret.Metadata.Should().Be("{}");
        secret.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void UpdateData_ShouldChangeEncryptedDataAndIv()
    {
        var secret = VaultSecret.Create("path", "old", "oldiv");

        secret.UpdateData("new", "newiv", "meta");

        secret.EncryptedData.Should().Be("new");
        secret.Iv.Should().Be("newiv");
        secret.Metadata.Should().Be("meta");
    }

    [Fact]
    public void UpdateData_WithoutMetadata_ShouldPreserveExisting()
    {
        var secret = VaultSecret.Create("path", "encrypted", "iv", metadata: "original");

        secret.UpdateData("new", "newiv");

        secret.Metadata.Should().Be("original");
    }

    [Fact]
    public void SoftDelete_ShouldSetDeletedAt()
    {
        var secret = VaultSecret.Create("path", "data", "iv");

        secret.SoftDelete();

        secret.DeletedAt.Should().NotBeNull();
        secret.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Restore_ShouldClearDeletedAt()
    {
        var secret = VaultSecret.Create("path", "data", "iv");
        secret.SoftDelete();

        secret.Restore();

        secret.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void SetLease_ShouldConfigureLeaseFields()
    {
        var secret = VaultSecret.Create("path", "data", "iv");

        secret.SetLease("lease_123", 3600, true);

        secret.LeaseId.Should().Be("lease_123");
        secret.LeaseTtl.Should().Be(3600);
        secret.LeaseRenewable.Should().BeTrue();
        secret.LeaseExpiresAt.Should().NotBeNull();
        secret.LeaseExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(3600), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CreateNextVersion_ShouldIncrementVersion()
    {
        var secret = VaultSecret.Create("path", "data", "iv", version: 3);

        var next = secret.CreateNextVersion("newdata", "newiv");

        next.Path.Should().Be("path");
        next.Version.Should().Be(4);
        next.EncryptedData.Should().Be("newdata");
        next.Iv.Should().Be("newiv");
    }
}

public class VaultTokenTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        var token = VaultToken.Create("hash123", "test-token", ["read-policy"], "service", 3600, 10);

        token.TokenHash.Should().Be("hash123");
        token.DisplayName.Should().Be("test-token");
        token.Policies.Should().Contain("read-policy");
        token.TokenType.Should().Be("service");
        token.Ttl.Should().Be(3600);
        token.NumUses.Should().Be(10);
        token.UsesCount.Should().Be(0);
        token.Revoked.Should().BeFalse();
        token.Accessor.Should().StartWith("accessor_");
        token.ExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public void Create_WithNoTtl_ShouldHaveNoExpiry()
    {
        var token = VaultToken.Create("hash", ttl: null);

        token.ExpiresAt.Should().BeNull();
        token.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void Create_WithDefaultPolicies_ShouldHaveDefault()
    {
        var token = VaultToken.Create("hash");

        token.Policies.Should().Contain("default");
    }

    [Fact]
    public void IncrementUse_ShouldIncreaseCount()
    {
        var token = VaultToken.Create("hash", numUses: 5);

        token.IncrementUse();
        token.IncrementUse();

        token.UsesCount.Should().Be(2);
        token.LastUsedAt.Should().NotBeNull();
    }

    [Fact]
    public void IsExhausted_ShouldBeTrueWhenUsesReachLimit()
    {
        var token = VaultToken.Create("hash", numUses: 2);

        token.IncrementUse();
        token.IsExhausted.Should().BeFalse();

        token.IncrementUse();
        token.IsExhausted.Should().BeTrue();
    }

    [Fact]
    public void IsExhausted_WithNoLimit_ShouldAlwaysBeFalse()
    {
        var token = VaultToken.Create("hash", numUses: null);

        for (int i = 0; i < 100; i++)
            token.IncrementUse();

        token.IsExhausted.Should().BeFalse();
    }

    [Fact]
    public void Revoke_ShouldMarkAsRevokedWithTimestamp()
    {
        var token = VaultToken.Create("hash");

        token.Revoke();

        token.Revoked.Should().BeTrue();
        token.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public void IsValid_ShouldBeFalseWhenRevoked()
    {
        var token = VaultToken.Create("hash");
        token.Revoke();

        token.IsValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_ShouldBeFalseWhenExhausted()
    {
        var token = VaultToken.Create("hash", numUses: 1);
        token.IncrementUse();

        token.IsValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_ShouldBeTrueForHealthyToken()
    {
        var token = VaultToken.Create("hash", numUses: 10, ttl: 3600);

        token.IsValid.Should().BeTrue();
    }
}

public class VaultPolicyTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        var policy = VaultPolicy.Create("admin-policy", "**", ["read", "create", "sudo"], "Full access");

        policy.Name.Should().Be("admin-policy");
        policy.PathPattern.Should().Be("**");
        policy.Capabilities.Should().HaveCount(3);
        policy.Description.Should().Be("Full access");
    }

    [Fact]
    public void Update_ShouldModifyPathAndCapabilities()
    {
        var policy = VaultPolicy.Create("test", "secrets/*", ["read"]);

        policy.Update("secrets/config/*", ["read", "update"], "Updated desc");

        policy.PathPattern.Should().Be("secrets/config/*");
        policy.Capabilities.Should().Contain("update");
        policy.Description.Should().Be("Updated desc");
    }

    [Fact]
    public void AllCapabilities_ShouldContainExpectedValues()
    {
        VaultPolicy.AllCapabilities.Should().Contain(["read", "create", "update", "delete", "list", "sudo"]);
    }
}

public class VaultLeaseTests
{
    [Fact]
    public void Create_ShouldSetLeaseIdAndExpiry()
    {
        var secretId = Guid.NewGuid();
        var lease = VaultLease.Create(secretId, 3600, true);

        lease.LeaseId.Should().StartWith("lease_");
        lease.SecretId.Should().Be(secretId);
        lease.Ttl.Should().Be(3600);
        lease.Renewable.Should().BeTrue();
        lease.Revoked.Should().BeFalse();
        lease.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(3600), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Renew_ShouldExtendExpiry()
    {
        var lease = VaultLease.Create(Guid.NewGuid(), 60, true);
        var originalExpiry = lease.ExpiresAt;

        lease.Renew(7200);

        lease.ExpiresAt.Should().BeAfter(originalExpiry);
        lease.Ttl.Should().Be(7200);
    }

    [Fact]
    public void Revoke_ShouldMarkAsRevoked()
    {
        var lease = VaultLease.Create(Guid.NewGuid(), 3600, true);

        lease.Revoke();

        lease.Revoked.Should().BeTrue();
        lease.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public void IsExpired_ShouldBeFalseForActiveLease()
    {
        var lease = VaultLease.Create(Guid.NewGuid(), 3600, true);

        lease.IsExpired.Should().BeFalse();
    }
}

public class VaultDynamicCredentialTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var cred = VaultDynamicCredential.Create(
            "postgres", "ivf_dyn_abc123", "localhost", 5432,
            "ivf_db", "admin", "encrypted_pwd", 3600);

        cred.Backend.Should().Be("postgres");
        cred.Username.Should().Be("ivf_dyn_abc123");
        cred.DbHost.Should().Be("localhost");
        cred.DbPort.Should().Be(5432);
        cred.DbName.Should().Be("ivf_db");
        cred.AdminUsername.Should().Be("admin");
        cred.AdminPasswordEncrypted.Should().Be("encrypted_pwd");
        cred.LeaseId.Should().StartWith("dynlease_");
        cred.Revoked.Should().BeFalse();
        cred.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(3600), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Revoke_ShouldMarkAsRevoked()
    {
        var cred = VaultDynamicCredential.Create(
            "postgres", "user", "host", 5432, "db", "admin", "pwd", 3600);

        cred.Revoke();

        cred.Revoked.Should().BeTrue();
        cred.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public void IsExpired_ShouldBeFalseForNewCredential()
    {
        var cred = VaultDynamicCredential.Create(
            "postgres", "user", "host", 5432, "db", "admin", "pwd", 3600);

        cred.IsExpired.Should().BeFalse();
    }
}
