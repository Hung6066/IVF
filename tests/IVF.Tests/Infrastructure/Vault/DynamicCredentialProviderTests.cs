using FluentAssertions;
using System.Reflection;

namespace IVF.Tests.Infrastructure.Vault;

/// <summary>
/// Tests for DynamicCredentialProvider static helper methods.
/// The main GenerateCredentialAsync/RevokeCredentialAsync methods require a live PostgreSQL connection
/// and should be tested via integration tests. These unit tests cover the security-critical helper logic.
/// </summary>
public class DynamicCredentialProviderTests
{
    private static readonly MethodInfo SanitizeMethod = typeof(IVF.Infrastructure.Services.DynamicCredentialProvider)
        .GetMethod("SanitizeIdentifier", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo BuildConnStringMethod = typeof(IVF.Infrastructure.Services.DynamicCredentialProvider)
        .GetMethod("BuildConnectionString", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static string SanitizeIdentifier(string input)
        => (string)SanitizeMethod.Invoke(null, [input])!;

    private static string BuildConnectionString(string host, int port, string dbName, string user, string pwd)
        => (string)BuildConnStringMethod.Invoke(null, [host, port, dbName, user, pwd])!;

    [Theory]
    [InlineData("valid_table", "valid_table")]
    [InlineData("public.users", "public.users")]
    [InlineData("my_table_123", "my_table_123")]
    public void SanitizeIdentifier_ValidNames_Unchanged(string input, string expected)
    {
        SanitizeIdentifier(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("table; DROP TABLE users--", "tableDROPTABLEusers")]
    [InlineData("tab'le", "table")]
    [InlineData("tab\"le", "table")]
    [InlineData("table\0name", "tablename")]
    [InlineData("table name", "tablename")]
    public void SanitizeIdentifier_MaliciousInput_StripsDangerousChars(string input, string expected)
    {
        SanitizeIdentifier(input).Should().Be(expected);
    }

    [Fact]
    public void SanitizeIdentifier_EmptyInput_ReturnsEmpty()
    {
        SanitizeIdentifier("").Should().BeEmpty();
    }

    [Fact]
    public void BuildConnectionString_ProducesValidFormat()
    {
        var result = BuildConnectionString("localhost", 5433, "ivf_db", "user1", "pass1");
        result.Should().Contain("Host=localhost");
        result.Should().Contain("Port=5433");
        result.Should().Contain("Database=ivf_db");
        result.Should().Contain("Username=user1");
        result.Should().Contain("Password=pass1");
    }
}
