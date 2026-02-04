using FluentAssertions;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Patients.Commands;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using Moq;

namespace IVF.Tests.Application;

public class PatientCommandsTests
{
    private readonly Mock<IPatientRepository> _patientRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    public PatientCommandsTests()
    {
        _patientRepoMock = new Mock<IPatientRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
    }

    [Fact]
    public async Task CreatePatient_ShouldReturnSuccessWithPatientDto()
    {
        // Arrange
        _patientRepoMock.Setup(r => r.GenerateCodeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("BN-2026-000001");
        _patientRepoMock.Setup(r => r.AddAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient p, CancellationToken _) => p);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new CreatePatientHandler(_patientRepoMock.Object, _unitOfWorkMock.Object);
        var command = new CreatePatientCommand(
            "Nguyen Van A",
            new DateTime(1990, 1, 15),
            Gender.Female,
            PatientType.Infertility,
            "012345678901",
            "0901234567",
            "123 Main St"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.PatientCode.Should().Be("BN-2026-000001");
        result.Value.FullName.Should().Be("Nguyen Van A");
        
        _patientRepoMock.Verify(r => r.AddAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdatePatient_WhenPatientNotFound_ShouldReturnFailure()
    {
        // Arrange
        _patientRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        var handler = new UpdatePatientHandler(_patientRepoMock.Object, _unitOfWorkMock.Object);
        var command = new UpdatePatientCommand(Guid.NewGuid(), "New Name", "0901234567", null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Patient not found");
    }

    [Fact]
    public async Task UpdatePatient_WhenPatientExists_ShouldReturnSuccess()
    {
        // Arrange
        var existingPatient = Patient.Create("BN-2026-000001", "Old Name", DateTime.Now.AddYears(-30), 
            Gender.Female, PatientType.Infertility);
        
        _patientRepoMock.Setup(r => r.GetByIdAsync(existingPatient.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPatient);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new UpdatePatientHandler(_patientRepoMock.Object, _unitOfWorkMock.Object);
        var command = new UpdatePatientCommand(existingPatient.Id, "New Name", "0901234567", "New Address");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.FullName.Should().Be("New Name");
        result.Value.Phone.Should().Be("0901234567");
    }

    [Fact]
    public async Task DeletePatient_WhenPatientExists_ShouldMarkAsDeleted()
    {
        // Arrange
        var existingPatient = Patient.Create("BN-2026-000001", "Test", DateTime.Now.AddYears(-30), 
            Gender.Female, PatientType.Infertility);
        
        _patientRepoMock.Setup(r => r.GetByIdAsync(existingPatient.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPatient);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new DeletePatientHandler(_patientRepoMock.Object, _unitOfWorkMock.Object);
        var command = new DeletePatientCommand(existingPatient.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        existingPatient.IsDeleted.Should().BeTrue();
        _patientRepoMock.Verify(r => r.UpdateAsync(existingPatient, It.IsAny<CancellationToken>()), Times.Once);
    }
}
