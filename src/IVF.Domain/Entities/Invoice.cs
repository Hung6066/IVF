using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

/// <summary>
/// Invoice for billing module
/// </summary>
public class Invoice : BaseEntity
{
    public string InvoiceNumber { get; private set; } = string.Empty;
    public Guid PatientId { get; private set; }
    public Guid? CycleId { get; private set; }
    public DateTime InvoiceDate { get; private set; }
    public DateTime? DueDate { get; private set; }
    
    public decimal SubTotal { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TaxPercent { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal BalanceDue => TotalAmount - PaidAmount;
    
    public InvoiceStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    
    // Navigation
    public Patient Patient { get; private set; } = null!;
    public TreatmentCycle? Cycle { get; private set; }
    public ICollection<InvoiceItem> Items { get; private set; } = new List<InvoiceItem>();
    public ICollection<Payment> Payments { get; private set; } = new List<Payment>();

    private Invoice() { }

    public static Invoice Create(
        string invoiceNumber,
        Guid patientId,
        DateTime invoiceDate,
        Guid? cycleId = null,
        DateTime? dueDate = null,
        Guid? createdByUserId = null)
    {
        return new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = invoiceNumber,
            PatientId = patientId,
            CycleId = cycleId,
            InvoiceDate = invoiceDate,
            DueDate = dueDate ?? invoiceDate.AddDays(30),
            Status = InvoiceStatus.Draft,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AddItem(string serviceCode, string description, int quantity, decimal unitPrice)
    {
        var item = InvoiceItem.Create(Id, serviceCode, description, quantity, unitPrice);
        Items.Add(item);
        RecalculateTotals();
    }

    public void RemoveItem(Guid itemId)
    {
        var item = Items.FirstOrDefault(i => i.Id == itemId);
        if (item != null)
        {
            Items.Remove(item);
            RecalculateTotals();
        }
    }

    public void ApplyDiscount(decimal percent)
    {
        DiscountPercent = percent;
        RecalculateTotals();
    }

    public void SetTax(decimal percent)
    {
        TaxPercent = percent;
        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        SubTotal = Items.Sum(i => i.Amount);
        DiscountAmount = SubTotal * (DiscountPercent / 100);
        var afterDiscount = SubTotal - DiscountAmount;
        TaxAmount = afterDiscount * (TaxPercent / 100);
        TotalAmount = afterDiscount + TaxAmount;
        SetUpdated();
    }

    public void Issue()
    {
        if (Status != InvoiceStatus.Draft)
            throw new InvalidOperationException("Only draft invoices can be issued");
        Status = InvoiceStatus.Issued;
        SetUpdated();
    }

    public void RecordPayment(decimal amount)
    {
        PaidAmount += amount;
        if (PaidAmount >= TotalAmount)
            Status = InvoiceStatus.Paid;
        else if (PaidAmount > 0)
            Status = InvoiceStatus.PartiallyPaid;
        SetUpdated();
    }

    public void Cancel()
    {
        Status = InvoiceStatus.Cancelled;
        SetUpdated();
    }
}

/// <summary>
/// Line item on an invoice
/// </summary>
public class InvoiceItem : BaseEntity
{
    public Guid InvoiceId { get; private set; }
    public string ServiceCode { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Amount => Quantity * UnitPrice;
    
    // Navigation
    public Invoice Invoice { get; private set; } = null!;

    private InvoiceItem() { }

    public static InvoiceItem Create(Guid invoiceId, string serviceCode, string description, int quantity, decimal unitPrice)
    {
        return new InvoiceItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            ServiceCode = serviceCode,
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(int quantity, decimal unitPrice)
    {
        Quantity = quantity;
        UnitPrice = unitPrice;
        SetUpdated();
    }
}

/// <summary>
/// Payment record
/// </summary>
public class Payment : BaseEntity
{
    public Guid InvoiceId { get; private set; }
    public string PaymentNumber { get; private set; } = string.Empty;
    public DateTime PaymentDate { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public string? TransactionReference { get; private set; }
    public string? Notes { get; private set; }
    public Guid? ReceivedByUserId { get; private set; }
    
    // Navigation
    public Invoice Invoice { get; private set; } = null!;

    private Payment() { }

    public static Payment Create(
        Guid invoiceId,
        string paymentNumber,
        DateTime paymentDate,
        decimal amount,
        PaymentMethod method,
        string? transactionReference = null,
        Guid? receivedByUserId = null)
    {
        return new Payment
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            PaymentNumber = paymentNumber,
            PaymentDate = paymentDate,
            Amount = amount,
            PaymentMethod = method,
            TransactionReference = transactionReference,
            ReceivedByUserId = receivedByUserId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
