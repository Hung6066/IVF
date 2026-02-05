# .NET Testing Patterns

Comprehensive testing strategies for enterprise .NET applications.

## Unit Testing Commands/Queries

```csharp
public class CreatePatientCommandTests
{
    private readonly Mock<IPatientRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CreatePatientCommandHandler _handler;

    public CreatePatientCommandTests()
    {
        _repositoryMock = new Mock<IPatientRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new CreatePatientCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidData_CreatesPatient()
    {
        // Arrange
        var command = new CreatePatientCommand(
            "John Doe",
            new DateOnly(1990, 1, 1),
            "john@example.com");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        _repositoryMock.Verify(r => r.AddAsync(
            It.Is<Patient>(p => p.FullName == "John Doe"),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Handle_WithInvalidName_ReturnsError(string? name)
    {
        // Arrange
        var command = new CreatePatientCommand(
            name!,
            new DateOnly(1990, 1, 1),
            "john@example.com");

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }
}
```

## Integration Testing with TestContainers

```csharp
[Collection("Database")]
public class PatientEndpointsTests : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client;
    private readonly ApiFixture _fixture;

    public PatientEndpointsTests(ApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", fixture.GetTestToken());
    }

    [Fact]
    public async Task CreatePatient_WithValidData_ReturnsCreated()
    {
        // Arrange
        var command = new CreatePatientCommand(
            "Integration Test Patient",
            new DateOnly(1990, 5, 15),
            "integration@test.com");

        // Act
        var response = await _client.PostAsJsonAsync("/api/patients", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBeEmpty();

        // Verify in database
        var getResponse = await _client.GetAsync($"/api/patients/{id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var patient = await getResponse.Content.ReadFromJsonAsync<PatientDto>();
        patient!.FullName.Should().Be("Integration Test Patient");
    }

    [Fact]
    public async Task GetPatients_WithSearchTerm_ReturnsFilteredResults()
    {
        // Arrange - create test data
        await _client.PostAsJsonAsync("/api/patients", new CreatePatientCommand(
            "Alice Smith", new DateOnly(1985, 1, 1), "alice@test.com"));
        await _client.PostAsJsonAsync("/api/patients", new CreatePatientCommand(
            "Bob Jones", new DateOnly(1990, 1, 1), "bob@test.com"));

        // Act
        var response = await _client.GetAsync("/api/patients?searchTerm=alice");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<PatientDto>>();
        result!.Items.Should().ContainSingle(p => p.FullName == "Alice Smith");
    }
}

public class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace database
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));

            // Apply migrations
            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        });

        builder.UseEnvironment("Testing");
    }

    public string GetTestToken() => JwtTestHelper.GenerateToken("test-user", ["Admin"]);

    public async Task InitializeAsync() => await _postgres.StartAsync();
    public new async Task DisposeAsync() => await _postgres.DisposeAsync();
}
```

## Domain Entity Testing

```csharp
public class TreatmentCycleTests
{
    [Fact]
    public void Create_WithValidData_CreatesEntity()
    {
        // Act
        var cycle = TreatmentCycle.Create(
            Guid.NewGuid(),
            CycleType.IVF,
            DateOnly.FromDateTime(DateTime.Today));

        // Assert
        cycle.Should().NotBeNull();
        cycle.CurrentPhase.Should().Be(CyclePhase.Stimulation);
        cycle.DomainEvents.Should().ContainSingle(e => e is CycleCreatedEvent);
    }

    [Theory]
    [InlineData(CyclePhase.Stimulation, CyclePhase.Trigger, true)]
    [InlineData(CyclePhase.Stimulation, CyclePhase.Transfer, false)]
    [InlineData(CyclePhase.Trigger, CyclePhase.Retrieval, true)]
    public void AdvanceToPhase_ValidatesTransition(CyclePhase from, CyclePhase to, bool shouldSucceed)
    {
        // Arrange
        var cycle = CreateCycleInPhase(from);

        // Act & Assert
        if (shouldSucceed)
        {
            cycle.AdvanceToPhase(to);
            cycle.CurrentPhase.Should().Be(to);
        }
        else
        {
            var act = () => cycle.AdvanceToPhase(to);
            act.Should().Throw<DomainException>();
        }
    }

    [Fact]
    public void AdvanceToPhase_RaisesDomainEvent()
    {
        // Arrange
        var cycle = CreateCycleInPhase(CyclePhase.Stimulation);

        // Act
        cycle.AdvanceToPhase(CyclePhase.Trigger);

        // Assert
        cycle.DomainEvents.Should().ContainSingle(e => 
            e is CyclePhaseChangedEvent changed &&
            changed.PreviousPhase == CyclePhase.Stimulation &&
            changed.NewPhase == CyclePhase.Trigger);
    }

    private static TreatmentCycle CreateCycleInPhase(CyclePhase phase)
    {
        var cycle = TreatmentCycle.Create(Guid.NewGuid(), CycleType.IVF, DateOnly.FromDateTime(DateTime.Today));
        // Simulate advancing to desired phase
        while (cycle.CurrentPhase != phase)
        {
            var nextPhase = GetNextPhase(cycle.CurrentPhase);
            cycle.AdvanceToPhase(nextPhase);
        }
        cycle.ClearDomainEvents();
        return cycle;
    }
}
```

## Validation Testing

```csharp
public class CreatePatientCommandValidatorTests
{
    private readonly CreatePatientCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidData_Succeeds()
    {
        var command = new CreatePatientCommand(
            "John Doe",
            new DateOnly(1990, 1, 1),
            "john@example.com");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Full Name must not be empty")]
    [InlineData(null, "Full Name must not be empty")]
    public void Validate_WithInvalidName_ReturnsError(string? name, string expectedError)
    {
        var command = new CreatePatientCommand(
            name!,
            new DateOnly(1990, 1, 1),
            "john@example.com");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(expectedError));
    }

    [Fact]
    public void Validate_WithFutureDateOfBirth_ReturnsError()
    {
        var command = new CreatePatientCommand(
            "John Doe",
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            "john@example.com");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DateOfBirth");
    }
}
```

## Test Data Builders

```csharp
public class PatientBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _fullName = "Test Patient";
    private DateOnly _dateOfBirth = new(1990, 1, 1);
    private string _email = "test@example.com";
    private PatientStatus _status = PatientStatus.Active;

    public PatientBuilder WithId(Guid id) { _id = id; return this; }
    public PatientBuilder WithName(string name) { _fullName = name; return this; }
    public PatientBuilder WithDateOfBirth(DateOnly dob) { _dateOfBirth = dob; return this; }
    public PatientBuilder WithEmail(string email) { _email = email; return this; }
    public PatientBuilder Inactive() { _status = PatientStatus.Inactive; return this; }

    public Patient Build()
    {
        var patient = Patient.Create(_fullName, _dateOfBirth, _email);
        if (_status == PatientStatus.Inactive) patient.Deactivate();
        return patient;
    }
}

// Usage
var patient = new PatientBuilder()
    .WithName("John Doe")
    .WithEmail("john@test.com")
    .Inactive()
    .Build();
```
