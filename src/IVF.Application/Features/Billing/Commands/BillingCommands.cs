using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Billing.Commands;

// ==================== CREATE INVOICE ====================
public record CreateInvoiceCommand(
    Guid PatientId,
    DateTime InvoiceDate,
    Guid? CycleId,
    Guid? CreatedByUserId
) : IRequest<Result<InvoiceDto>>;

public class CreateInvoiceHandler : IRequestHandler<CreateInvoiceCommand, Result<InvoiceDto>>
{
    private readonly IInvoiceRepository _invoiceRepo;
    private readonly IPatientRepository _patientRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateInvoiceHandler(IInvoiceRepository invoiceRepo, IPatientRepository patientRepo, IUnitOfWork unitOfWork)
    {
        _invoiceRepo = invoiceRepo;
        _patientRepo = patientRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<InvoiceDto>> Handle(CreateInvoiceCommand request, CancellationToken ct)
    {
        var patient = await _patientRepo.GetByIdAsync(request.PatientId, ct);
        if (patient == null) return Result<InvoiceDto>.Failure("Patient not found");

        var invoiceNumber = await _invoiceRepo.GenerateInvoiceNumberAsync(ct);
        var invoice = Invoice.Create(invoiceNumber, request.PatientId, request.InvoiceDate, request.CycleId, null, request.CreatedByUserId);
        await _invoiceRepo.AddAsync(invoice, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<InvoiceDto>.Success(InvoiceDto.FromEntity(invoice, patient.FullName));
    }
}

// ==================== ADD ITEM ====================
public record AddInvoiceItemCommand(
    Guid InvoiceId,
    string ServiceCode,
    string Description,
    int Quantity,
    decimal UnitPrice
) : IRequest<Result<InvoiceDto>>;

public class AddInvoiceItemHandler : IRequestHandler<AddInvoiceItemCommand, Result<InvoiceDto>>
{
    private readonly IInvoiceRepository _invoiceRepo;
    private readonly IUnitOfWork _unitOfWork;

    public AddInvoiceItemHandler(IInvoiceRepository invoiceRepo, IUnitOfWork unitOfWork)
    {
        _invoiceRepo = invoiceRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<InvoiceDto>> Handle(AddInvoiceItemCommand r, CancellationToken ct)
    {
        var invoice = await _invoiceRepo.GetByIdWithItemsAsync(r.InvoiceId, ct);
        if (invoice == null) return Result<InvoiceDto>.Failure("Invoice not found");

        invoice.AddItem(r.ServiceCode, r.Description, r.Quantity, r.UnitPrice);
        await _invoiceRepo.UpdateAsync(invoice, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<InvoiceDto>.Success(InvoiceDto.FromEntity(invoice, invoice.Patient?.FullName ?? ""));
    }
}

// ==================== ISSUE INVOICE ====================
public record IssueInvoiceCommand(Guid InvoiceId) : IRequest<Result<InvoiceDto>>;

public class IssueInvoiceHandler : IRequestHandler<IssueInvoiceCommand, Result<InvoiceDto>>
{
    private readonly IInvoiceRepository _invoiceRepo;
    private readonly IUnitOfWork _unitOfWork;

    public IssueInvoiceHandler(IInvoiceRepository invoiceRepo, IUnitOfWork unitOfWork)
    {
        _invoiceRepo = invoiceRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<InvoiceDto>> Handle(IssueInvoiceCommand request, CancellationToken ct)
    {
        var invoice = await _invoiceRepo.GetByIdWithItemsAsync(request.InvoiceId, ct);
        if (invoice == null) return Result<InvoiceDto>.Failure("Invoice not found");

        invoice.Issue();
        await _invoiceRepo.UpdateAsync(invoice, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<InvoiceDto>.Success(InvoiceDto.FromEntity(invoice, invoice.Patient?.FullName ?? ""));
    }
}

// ==================== RECORD PAYMENT ====================
public record RecordPaymentCommand(
    Guid InvoiceId,
    decimal Amount,
    PaymentMethod PaymentMethod,
    string? TransactionReference,
    Guid? ReceivedByUserId
) : IRequest<Result<PaymentDto>>;

public class RecordPaymentHandler : IRequestHandler<RecordPaymentCommand, Result<PaymentDto>>
{
    private readonly IInvoiceRepository _invoiceRepo;
    private readonly IPaymentRepository _paymentRepo;
    private readonly IUnitOfWork _unitOfWork;

    public RecordPaymentHandler(IInvoiceRepository invoiceRepo, IPaymentRepository paymentRepo, IUnitOfWork unitOfWork)
    {
        _invoiceRepo = invoiceRepo;
        _paymentRepo = paymentRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PaymentDto>> Handle(RecordPaymentCommand r, CancellationToken ct)
    {
        var invoice = await _invoiceRepo.GetByIdAsync(r.InvoiceId, ct);
        if (invoice == null) return Result<PaymentDto>.Failure("Invoice not found");

        var paymentNumber = await _paymentRepo.GeneratePaymentNumberAsync(ct);
        var payment = Payment.Create(r.InvoiceId, paymentNumber, DateTime.UtcNow, r.Amount, r.PaymentMethod, r.TransactionReference, r.ReceivedByUserId);
        
        invoice.RecordPayment(r.Amount);
        
        await _paymentRepo.AddAsync(payment, ct);
        await _invoiceRepo.UpdateAsync(invoice, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<PaymentDto>.Success(PaymentDto.FromEntity(payment));
    }
}

// ==================== DTOs ====================
public record InvoiceDto(
    Guid Id,
    string InvoiceNumber,
    Guid PatientId,
    string PatientName,
    DateTime InvoiceDate,
    string Status,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceDue,
    int ItemCount,
    DateTime CreatedAt
)
{
    public static InvoiceDto FromEntity(Invoice i, string patientName) => new(
        i.Id, i.InvoiceNumber, i.PatientId, patientName, i.InvoiceDate, i.Status.ToString(),
        i.SubTotal, i.DiscountAmount, i.TaxAmount, i.TotalAmount, i.PaidAmount, i.BalanceDue,
        i.Items?.Count ?? 0, i.CreatedAt
    );
}

public record PaymentDto(
    Guid Id,
    string PaymentNumber,
    Guid InvoiceId,
    DateTime PaymentDate,
    decimal Amount,
    string PaymentMethod,
    string? TransactionReference,
    DateTime CreatedAt
)
{
    public static PaymentDto FromEntity(Payment p) => new(
        p.Id, p.PaymentNumber, p.InvoiceId, p.PaymentDate, p.Amount,
        p.PaymentMethod.ToString(), p.TransactionReference, p.CreatedAt
    );
}
