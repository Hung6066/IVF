using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IVF.Application.Features.Queue.Commands;

// ==================== ISSUE TICKET ====================
public record IssueTicketCommand(
    Guid PatientId,
    TicketPriority Priority,
    string DepartmentCode,
    Guid? CycleId,
    List<Guid>? ServiceIds
) : IRequest<Result<QueueTicketDto>>;

public class IssueTicketValidator : AbstractValidator<IssueTicketCommand>
{
    public IssueTicketValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.DepartmentCode).NotEmpty().MaximumLength(20);
    }
}

public class IssueTicketHandler : IRequestHandler<IssueTicketCommand, Result<QueueTicketDto>>
{
    private readonly IQueueTicketRepository _queueRepo;
    private readonly IPatientRepository _patientRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<IssueTicketHandler> _logger; // Added logger

    public IssueTicketHandler(IQueueTicketRepository queueRepo, IPatientRepository patientRepo, IUnitOfWork unitOfWork, ILogger<IssueTicketHandler> logger)
    {
        _queueRepo = queueRepo;
        _patientRepo = patientRepo;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<QueueTicketDto>> Handle(IssueTicketCommand request, CancellationToken ct)
    {
        _logger.LogInformation("IssueTicket: Dept={Dept}, Priority={Priority}, ServicesCount={Count}", 
            request.DepartmentCode, request.Priority, request.ServiceIds?.Count ?? 0);

        var patient = await _patientRepo.GetByIdAsync(request.PatientId, ct);
        if (patient == null)
            return Result<QueueTicketDto>.Failure("Patient not found");

        var ticketNumber = await _queueRepo.GenerateTicketNumberAsync(request.DepartmentCode, ct);

        var queueType = GetQueueType(request.DepartmentCode);
        
        var ticket = QueueTicket.Create(ticketNumber, queueType, request.Priority, request.PatientId, request.DepartmentCode, request.CycleId, request.ServiceIds);
        
        await _queueRepo.AddAsync(ticket, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<QueueTicketDto>.Success(QueueTicketDto.FromEntity(ticket, patient.FullName, patient.PatientCode));
    }

    private QueueType GetQueueType(string deptCode)
    {
        return deptCode.ToUpper() switch
        {
            "TV" => QueueType.Consultation,
            "US" => QueueType.Ultrasound,
            "XN" => QueueType.LabTest,
            "LAB" => QueueType.LabTest,
            "NAM" => QueueType.Andrology,
            "AND" => QueueType.Andrology,
            "TM" => QueueType.Injection,
            "NT" => QueueType.Pharmacy,
            _ => QueueType.Reception
        };
    }
}

// ==================== CALL TICKET ====================
public record CallTicketCommand(Guid TicketId, Guid UserId) : IRequest<Result<QueueTicketDto>>;

public class CallTicketHandler : IRequestHandler<CallTicketCommand, Result<QueueTicketDto>>
{
    private readonly IQueueTicketRepository _queueRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CallTicketHandler(IQueueTicketRepository queueRepo, IUnitOfWork unitOfWork)
    {
        _queueRepo = queueRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<QueueTicketDto>> Handle(CallTicketCommand request, CancellationToken ct)
    {
        var ticket = await _queueRepo.GetByIdAsync(request.TicketId, ct);
        if (ticket == null)
            return Result<QueueTicketDto>.Failure("Ticket not found");

        ticket.Call(request.UserId);
        await _queueRepo.UpdateAsync(ticket, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<QueueTicketDto>.Success(QueueTicketDto.FromEntity(ticket, ticket.Patient?.FullName ?? "", ticket.Patient?.PatientCode));
    }
}

// Start Service Command
public record StartServiceCommand(Guid TicketId) : IRequest<Result<QueueTicketDto>>;

public class StartServiceHandler : IRequestHandler<StartServiceCommand, Result<QueueTicketDto>>
{
    private readonly IQueueTicketRepository _queueRepo;
    private readonly IUnitOfWork _unitOfWork;

    public StartServiceHandler(IQueueTicketRepository queueRepo, IUnitOfWork unitOfWork)
    {
        _queueRepo = queueRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<QueueTicketDto>> Handle(StartServiceCommand request, CancellationToken ct)
    {
        var ticket = await _queueRepo.GetByIdAsync(request.TicketId, ct);
        if (ticket == null)
            return Result<QueueTicketDto>.Failure("Ticket not found");

        ticket.StartService();
        await _queueRepo.UpdateAsync(ticket, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<QueueTicketDto>.Success(QueueTicketDto.FromEntity(ticket, ticket.Patient?.FullName ?? "", ticket.Patient?.PatientCode));
    }
}

// ==================== COMPLETE TICKET ====================
public record CompleteTicketCommand(Guid TicketId, string? Notes = null) : IRequest<Result>;

public class CompleteTicketHandler : IRequestHandler<CompleteTicketCommand, Result>
{
    private readonly IQueueTicketRepository _queueRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteTicketHandler(IQueueTicketRepository queueRepo, IUnitOfWork unitOfWork)
    {
        _queueRepo = queueRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CompleteTicketCommand request, CancellationToken ct)
    {
        var ticket = await _queueRepo.GetByIdAsync(request.TicketId, ct);
        if (ticket == null)
            return Result.Failure("Ticket not found");

        ticket.Complete();
        if (!string.IsNullOrEmpty(request.Notes))
            ticket.Notes = request.Notes;
        await _queueRepo.UpdateAsync(ticket, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

// ==================== SKIP TICKET ====================
public record SkipTicketCommand(Guid TicketId) : IRequest<Result>;

public class SkipTicketHandler : IRequestHandler<SkipTicketCommand, Result>
{
    private readonly IQueueTicketRepository _queueRepo;
    private readonly IUnitOfWork _unitOfWork;

    public SkipTicketHandler(IQueueTicketRepository queueRepo, IUnitOfWork unitOfWork)
    {
        _queueRepo = queueRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(SkipTicketCommand request, CancellationToken ct)
    {
        var ticket = await _queueRepo.GetByIdAsync(request.TicketId, ct);
        if (ticket == null)
            return Result.Failure("Ticket not found");

        ticket.Skip();
        await _queueRepo.UpdateAsync(ticket, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

// ==================== DTO ====================
public record QueueTicketDto(
    Guid Id,
    string TicketNumber,
    Guid PatientId,
    string PatientCode,
    string PatientName,
    string QueueType,
    string DepartmentCode,
    string Status,
    DateTime IssuedAt,
    DateTime? CalledAt,
    DateTime? CompletedAt,
    string? Notes,
    List<Guid>? ServiceIds
)
{
    public static QueueTicketDto FromEntity(QueueTicket t, string patientName, string? patientCode = null)
    {
        var serviceIds = t.Services?.Select(s => s.ServiceCatalogId).ToList();
        return new(
            t.Id, t.TicketNumber, t.PatientId, patientCode ?? "", patientName,
            t.QueueType.ToString(), t.DepartmentCode, t.Status.ToString(),
            t.IssuedAt, t.CalledAt, t.CompletedAt, t.Notes, serviceIds
        );
    }
}
