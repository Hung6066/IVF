using FluentAssertions;
using IVF.Domain.Entities;
using IVF.Domain.Enums;

namespace IVF.Tests.Domain;

public class PatientTests
{
    [Fact]
    public void Create_ShouldReturnPatientWithCorrectProperties()
    {
        // Arrange
        var code = "BN-2026-000001";
        var name = "Nguyen Van A";
        var dob = new DateTime(1990, 1, 15);
        var gender = Gender.Female;
        var type = PatientType.Infertility;
        var phone = "0901234567";

        // Act
        var patient = Patient.Create(code, name, dob, gender, type, null, phone, null);

        // Assert
        patient.PatientCode.Should().Be(code);
        patient.FullName.Should().Be(name);
        patient.DateOfBirth.Should().Be(dob);
        patient.Gender.Should().Be(gender);
        patient.PatientType.Should().Be(type);
        patient.Phone.Should().Be(phone);
        patient.IsDeleted.Should().BeFalse();
        patient.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Update_ShouldModifyPatientProperties()
    {
        // Arrange
        var patient = Patient.Create("BN-2026-000001", "Nguyen Van A", DateTime.Now.AddYears(-30), 
            Gender.Female, PatientType.Infertility);
        var newName = "Nguyen Thi B";
        var newPhone = "0987654321";
        var newAddress = "123 Main St";

        // Act
        patient.Update(newName, newPhone, newAddress);

        // Assert
        patient.FullName.Should().Be(newName);
        patient.Phone.Should().Be(newPhone);
        patient.Address.Should().Be(newAddress);
        patient.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsDeleted_ShouldSetIsDeletedToTrue()
    {
        // Arrange
        var patient = Patient.Create("BN-2026-000001", "Nguyen Van A", DateTime.Now.AddYears(-30), 
            Gender.Female, PatientType.Infertility);

        // Act
        patient.MarkAsDeleted();

        // Assert
        patient.IsDeleted.Should().BeTrue();
        patient.UpdatedAt.Should().NotBeNull();
    }
}

public class TreatmentCycleTests
{
    [Fact]
    public void Create_ShouldInitializeWithConsultationPhase()
    {
        // Arrange
        var coupleId = Guid.NewGuid();
        var code = "CK-2026-0001";

        // Act
        var cycle = TreatmentCycle.Create(coupleId, code, TreatmentMethod.ICSI, DateTime.Today);

        // Assert
        cycle.CurrentPhase.Should().Be(CyclePhase.Consultation);
        cycle.Outcome.Should().Be(CycleOutcome.Ongoing);
        cycle.CoupleId.Should().Be(coupleId);
    }

    [Fact]
    public void AdvancePhase_ShouldUpdatePhaseAndTimestamp()
    {
        // Arrange
        var cycle = TreatmentCycle.Create(Guid.NewGuid(), "CK-2026-0001", TreatmentMethod.ICSI, DateTime.Today);

        // Act
        cycle.AdvancePhase(CyclePhase.OvarianStimulation);

        // Assert
        cycle.CurrentPhase.Should().Be(CyclePhase.OvarianStimulation);
        cycle.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Complete_ShouldSetOutcomeAndEndDate()
    {
        // Arrange
        var cycle = TreatmentCycle.Create(Guid.NewGuid(), "CK-2026-0001", TreatmentMethod.IUI, DateTime.Today);

        // Act
        cycle.Complete(CycleOutcome.Pregnant);

        // Assert
        cycle.Outcome.Should().Be(CycleOutcome.Pregnant);
        cycle.CurrentPhase.Should().Be(CyclePhase.Completed);
        cycle.EndDate.Should().NotBeNull();
    }
}

public class QueueTicketTests
{
    [Fact]
    public void Create_ShouldInitializeWithWaitingStatus()
    {
        // Arrange & Act
        var ticket = QueueTicket.Create("TD-001", QueueType.Reception, TicketPriority.Normal, Guid.NewGuid(), "TD-01");

        // Assert
        ticket.Status.Should().Be(TicketStatus.Waiting);
        ticket.IssuedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Call_ShouldUpdateStatusAndSetCalledTime()
    {
        // Arrange
        var ticket = QueueTicket.Create("TD-001", QueueType.Reception, TicketPriority.Normal, Guid.NewGuid(), "TD-01");
        var userId = Guid.NewGuid();

        // Act
        ticket.Call(userId);

        // Assert
        ticket.Status.Should().Be(TicketStatus.Called);
        ticket.CalledAt.Should().NotBeNull();
        ticket.CalledByUserId.Should().Be(userId);
    }

    [Fact]
    public void Complete_ShouldSetCompletedStatus()
    {
        // Arrange
        var ticket = QueueTicket.Create("TD-001", QueueType.Reception, TicketPriority.Normal, Guid.NewGuid(), "TD-01");
        ticket.Call(Guid.NewGuid());

        // Act
        ticket.Complete();

        // Assert
        ticket.Status.Should().Be(TicketStatus.Completed);
        ticket.CompletedAt.Should().NotBeNull();
    }
}

public class EmbryoTests
{
    [Fact]
    public void Create_ShouldInitializeWithDevelopingStatus()
    {
        // Arrange & Act
        var embryo = Embryo.Create(Guid.NewGuid(), 1, DateTime.Today);

        // Assert
        embryo.Status.Should().Be(EmbryoStatus.Developing);
        embryo.Day.Should().Be(EmbryoDay.D1);
    }

    [Fact]
    public void Freeze_ShouldUpdateStatusAndLocation()
    {
        // Arrange
        var embryo = Embryo.Create(Guid.NewGuid(), 1, DateTime.Today);
        var locationId = Guid.NewGuid();

        // Act
        embryo.Freeze(locationId);

        // Assert
        embryo.Status.Should().Be(EmbryoStatus.Frozen);
        embryo.CryoLocationId.Should().Be(locationId);
        embryo.FreezeDate.Should().NotBeNull();
    }

    [Fact]
    public void Transfer_ShouldUpdateStatus()
    {
        // Arrange
        var embryo = Embryo.Create(Guid.NewGuid(), 1, DateTime.Today);

        // Act
        embryo.Transfer();

        // Assert
        embryo.Status.Should().Be(EmbryoStatus.Transferred);
    }
}
