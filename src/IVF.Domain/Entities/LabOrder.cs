using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class LabOrder : BaseEntity, ITenantEntity
{
    public Guid PatientId { get; private set; }
    public Guid? CycleId { get; private set; }
    public Guid OrderedByUserId { get; private set; }
    public Guid TenantId { get; private set; }
    public void SetTenantId(Guid tenantId) { TenantId = tenantId; SetUpdated(); }
    public DateTime OrderedAt { get; private set; }
    public string OrderType { get; private set; } = string.Empty; // ROUTINE, HORMONAL, PRE_ANESTHESIA, BETA_HCG, HIV_SCREENING, BLOOD_TYPE
    public string Status { get; private set; } = "Ordered"; // Ordered, SampleCollected, InProgress, Completed, Delivered
    public string? ResultDeliveredTo { get; private set; } // Patient, Doctor, Nurse
    public DateTime? CompletedAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public Guid? DeliveredByUserId { get; private set; }
    public string? Notes { get; private set; }

    // Navigation properties
    public virtual Patient Patient { get; private set; } = null!;
    public virtual TreatmentCycle? Cycle { get; private set; }
    public virtual User OrderedBy { get; private set; } = null!;
    public virtual ICollection<LabTest> Tests { get; private set; } = new List<LabTest>();

    private LabOrder() { }

    public static LabOrder Create(
        Guid patientId,
        Guid orderedByUserId,
        string orderType,
        Guid? cycleId = null,
        string? notes = null)
    {
        return new LabOrder
        {
            PatientId = patientId,
            OrderedByUserId = orderedByUserId,
            OrderType = orderType,
            OrderedAt = DateTime.UtcNow,
            CycleId = cycleId,
            Notes = notes
        };
    }

    public void CollectSample()
    {
        Status = "SampleCollected";
        SetUpdated();
    }

    public void StartProcessing()
    {
        Status = "InProgress";
        SetUpdated();
    }

    public void Complete()
    {
        Status = "Completed";
        CompletedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Deliver(Guid deliveredByUserId, string deliveredTo)
    {
        Status = "Delivered";
        DeliveredAt = DateTime.UtcNow;
        DeliveredByUserId = deliveredByUserId;
        ResultDeliveredTo = deliveredTo;
        SetUpdated();
    }

    public void AddTest(LabTest test)
    {
        Tests.Add(test);
        SetUpdated();
    }
}

public class LabTest : BaseEntity
{
    public Guid LabOrderId { get; private set; }
    public string TestCode { get; private set; } = string.Empty;
    public string TestName { get; private set; } = string.Empty;
    public string? ResultValue { get; private set; }
    public string? ResultUnit { get; private set; }
    public string? ReferenceRange { get; private set; }
    public bool IsAbnormal { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Guid? PerformedByUserId { get; private set; }
    public string? Notes { get; private set; }

    // Navigation
    public virtual LabOrder LabOrder { get; private set; } = null!;

    private LabTest() { }

    public static LabTest Create(
        Guid labOrderId,
        string testCode,
        string testName,
        string? referenceRange = null)
    {
        return new LabTest
        {
            LabOrderId = labOrderId,
            TestCode = testCode,
            TestName = testName,
            ReferenceRange = referenceRange
        };
    }

    public void RecordResult(string resultValue, string? resultUnit, bool isAbnormal, Guid performedByUserId, string? notes = null)
    {
        ResultValue = resultValue;
        ResultUnit = resultUnit;
        IsAbnormal = isAbnormal;
        PerformedByUserId = performedByUserId;
        CompletedAt = DateTime.UtcNow;
        Notes = notes;
        SetUpdated();
    }
}
