using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Billing.Commands;
using MediatR;

namespace IVF.Application.Features.Billing.Queries;

// ==================== GET INVOICE BY ID ====================
public record GetInvoiceByIdQuery(Guid Id) : IRequest<Result<InvoiceDetailDto>>;

public class GetInvoiceByIdHandler : IRequestHandler<GetInvoiceByIdQuery, Result<InvoiceDetailDto>>
{
    private readonly IInvoiceRepository _invoiceRepo;

    public GetInvoiceByIdHandler(IInvoiceRepository invoiceRepo) => _invoiceRepo = invoiceRepo;

    public async Task<Result<InvoiceDetailDto>> Handle(GetInvoiceByIdQuery request, CancellationToken ct)
    {
        var invoice = await _invoiceRepo.GetByIdWithItemsAsync(request.Id, ct);
        if (invoice == null) return Result<InvoiceDetailDto>.Failure("Invoice not found");
        return Result<InvoiceDetailDto>.Success(InvoiceDetailDto.FromEntity(invoice));
    }
}

// ==================== GET INVOICES BY PATIENT ====================
public record GetInvoicesByPatientQuery(Guid PatientId) : IRequest<IReadOnlyList<InvoiceDto>>;

public class GetInvoicesByPatientHandler : IRequestHandler<GetInvoicesByPatientQuery, IReadOnlyList<InvoiceDto>>
{
    private readonly IInvoiceRepository _invoiceRepo;

    public GetInvoicesByPatientHandler(IInvoiceRepository invoiceRepo) => _invoiceRepo = invoiceRepo;

    public async Task<IReadOnlyList<InvoiceDto>> Handle(GetInvoicesByPatientQuery request, CancellationToken ct)
    {
        var invoices = await _invoiceRepo.GetByPatientIdAsync(request.PatientId, ct);
        return invoices.Select(i => InvoiceDto.FromEntity(i, i.Patient?.FullName ?? "")).ToList();
    }
}

// ==================== SEARCH INVOICES ====================
public record SearchInvoicesQuery(string? Query, int Page = 1, int PageSize = 20) : IRequest<PagedResult<InvoiceDto>>;

public class SearchInvoicesHandler : IRequestHandler<SearchInvoicesQuery, PagedResult<InvoiceDto>>
{
    private readonly IInvoiceRepository _invoiceRepo;

    public SearchInvoicesHandler(IInvoiceRepository invoiceRepo) => _invoiceRepo = invoiceRepo;

    public async Task<PagedResult<InvoiceDto>> Handle(SearchInvoicesQuery request, CancellationToken ct)
    {
        var (items, total) = await _invoiceRepo.SearchAsync(request.Query, request.Page, request.PageSize, ct);
        var dtos = items.Select(i => InvoiceDto.FromEntity(i, i.Patient?.FullName ?? "")).ToList();
        return new PagedResult<InvoiceDto>(dtos, total, request.Page, request.PageSize);
    }
}

// ==================== DETAIL DTO ====================
public record InvoiceDetailDto(
    Guid Id,
    string InvoiceNumber,
    Guid PatientId,
    string PatientName,
    DateTime InvoiceDate,
    string Status,
    decimal SubTotal,
    decimal DiscountPercent,
    decimal DiscountAmount,
    decimal TaxPercent,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceDue,
    IReadOnlyList<InvoiceItemDto> Items,
    DateTime CreatedAt
)
{
    public static InvoiceDetailDto FromEntity(Domain.Entities.Invoice i) => new(
        i.Id, i.InvoiceNumber, i.PatientId, i.Patient?.FullName ?? "", i.InvoiceDate, i.Status.ToString(),
        i.SubTotal, i.DiscountPercent, i.DiscountAmount, i.TaxPercent, i.TaxAmount, i.TotalAmount, i.PaidAmount, i.BalanceDue,
        i.Items?.Select(InvoiceItemDto.FromEntity).ToList() ?? new List<InvoiceItemDto>(), i.CreatedAt
    );
}

public record InvoiceItemDto(Guid Id, string ServiceCode, string Description, int Quantity, decimal UnitPrice, decimal Amount)
{
    public static InvoiceItemDto FromEntity(Domain.Entities.InvoiceItem i) => new(
        i.Id, i.ServiceCode, i.Description, i.Quantity, i.UnitPrice, i.Amount
    );
}
