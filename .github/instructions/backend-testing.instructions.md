---
description: "Use when writing or modifying backend unit tests. Enforces xUnit + Moq + FluentAssertions conventions, Arrange/Act/Assert pattern, and test naming standards for the IVF .NET 10 project."
applyTo: "tests/**"
---

# Backend Testing Conventions

## Framework Stack

- **xUnit** — test framework (global using via csproj `<Using Include="Xunit" />`)
- **Moq** — mocking (`Mock<T>`, `Setup`, `Verify`)
- **FluentAssertions** — assertions (`.Should()` chains)

## Test Naming

```
{Method}_Should{Expected}
{Method}_When{Condition}_Should{Expected}
```

Examples:

- `CreatePatient_ShouldReturnSuccessWithPatientDto`
- `UpdatePatient_WhenPatientNotFound_ShouldReturnFailure`
- `MarkAsDeleted_ShouldSetIsDeletedToTrue`

## File & Class Naming

- File: `tests/IVF.Tests/{Layer}/{Feature}Tests.cs`
- Class: `{Feature}Tests` (e.g., `PatientCommandsTests`, `PatientTests`, `VaultSecretTests`)
- Namespace: `IVF.Tests.{Layer}` (e.g., `IVF.Tests.Application`, `IVF.Tests.Domain`)
- One test class per feature or entity — multiple `[Fact]` methods inside

## Structure Pattern

```csharp
using FluentAssertions;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.{Feature}.Commands;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using Moq;

namespace IVF.Tests.{Layer};

public class {Feature}Tests
{
    // Mock fields — setup in constructor
    private readonly Mock<I{Entity}Repository> _repoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    public {Feature}Tests()
    {
        _repoMock = new Mock<I{Entity}Repository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
    }

    [Fact]
    public async Task Create{Entity}_ShouldReturnSuccessWithDto()
    {
        // Arrange
        _repoMock.Setup(r => r.AddAsync(It.IsAny<{Entity}>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Entity e, CancellationToken _) => e);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new Create{Entity}Handler(_repoMock.Object, _unitOfWorkMock.Object);
        var command = new Create{Entity}Command(...);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        _repoMock.Verify(r => r.AddAsync(It.IsAny<{Entity}>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

## Key Rules

1. **Always use `// Arrange`, `// Act`, `// Assert` comments** — every test method
2. **Mock setup in constructor** — shared mocks as `private readonly` fields
3. **Handlers instantiated manually** — no DI container; pass `mock.Object` directly
4. **Use `CancellationToken.None`** in all handler calls
5. **Result assertions**: `result.IsSuccess.Should().BeTrue()` / `.BeFalse()`, `result.Error.Should().Be("...")`
6. **Verify repository calls**: `_repoMock.Verify(r => r.Method(...), Times.Once)`
7. **Domain entity tests**: use `Entity.Create(...)` factory; assert properties directly
8. **Time assertions**: use `.BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5))`
9. **No `[Theory]` used** — project uses `[Fact]` exclusively with inline values
10. **Async handlers**: return `Task`, use `async Task` test methods with `await`

## Run Commands

```bash
dotnet test tests/IVF.Tests/IVF.Tests.csproj
dotnet test --filter "FullyQualifiedName~PatientCommandsTests"
```
