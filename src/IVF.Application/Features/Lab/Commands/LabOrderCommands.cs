using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Attributes;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Lab.Commands;

// ==================== Lab Order DTOs ====================
public record LabOrderDto(
    Guid Id,
    Guid PatientId,
    string PatientName,
    string PatientCode,
    Guid? CycleId,
    Guid OrderedByUserId,
    string OrderedByName,
    DateTime OrderedAt,
    string OrderType,
    string Status,
    string? ResultDeliveredTo,
    DateTime? CompletedAt,
    DateTime? DeliveredAt,
    string? Notes,
    DateTime CreatedAt,
    List<LabTestDto> Tests)
{
    public static LabOrderDto FromEntity(LabOrder o) => new(
        o.Id,
        o.PatientId,
        o.Patient?.FullName ?? "",
        o.Patient?.PatientCode ?? "",
        o.CycleId,
        o.OrderedByUserId,
        o.OrderedBy?.FullName ?? "",
        o.OrderedAt,
        o.OrderType,
        o.Status,
        o.ResultDeliveredTo,
        o.CompletedAt,
        o.DeliveredAt,
        o.Notes,
        o.CreatedAt,
        o.Tests?.Select(LabTestDto.FromEntity).ToList() ?? []);
}

public record LabTestDto(
    Guid Id,
    string TestCode,
    string TestName,
    string? ResultValue,
    string? ResultUnit,
    string? ReferenceRange,
    bool IsAbnormal,
    DateTime? CompletedAt,
    string? Notes)
{
    public static LabTestDto FromEntity(LabTest t) => new(
        t.Id,
        t.TestCode,
        t.TestName,
        t.ResultValue,
        t.ResultUnit,
        t.ReferenceRange,
        t.IsAbnormal,
        t.CompletedAt,
        t.Notes);
}

// ==================== CREATE LAB ORDER ====================
public record LabTestInput(string TestCode, string TestName, string? ReferenceRange = null);

[RequiresFeature(FeatureCodes.Lab)]
public record CreateLabOrderCommand(
    Guid PatientId,
    Guid OrderedByUserId,
    string OrderType,
    Guid? CycleId,
    string? Notes,
    List<LabTestInput> Tests
) : IRequest<Result<LabOrderDto>>;

public class CreateLabOrderValidator : AbstractValidator<CreateLabOrderCommand>
{
    public CreateLabOrderValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty().WithMessage("Patient ID is required");
        RuleFor(x => x.OrderedByUserId).NotEmpty().WithMessage("Ordered by user ID is required");
        RuleFor(x => x.OrderType).NotEmpty().WithMessage("Order type is required");
        RuleFor(x => x.Tests).NotEmpty().WithMessage("At least one test is required");
        RuleForEach(x => x.Tests).ChildRules(t =>
        {
            t.RuleFor(i => i.TestCode).NotEmpty().WithMessage("Test code is required");
            t.RuleFor(i => i.TestName).NotEmpty().WithMessage("Test name is required");
        });
    }
}

public class CreateLabOrderHandler : IRequestHandler<CreateLabOrderCommand, Result<LabOrderDto>>
{
    private readonly ILabOrderRepository _labOrderRepo;
    private readonly IPatientRepository _patientRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateLabOrderHandler(ILabOrderRepository labOrderRepo, IPatientRepository patientRepo, IUnitOfWork unitOfWork)
    {
        _labOrderRepo = labOrderRepo;
        _patientRepo = patientRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<LabOrderDto>> Handle(CreateLabOrderCommand request, CancellationToken ct)
    {
        var patient = await _patientRepo.GetByIdAsync(request.PatientId, ct);
        if (patient == null)
            return Result<LabOrderDto>.Failure($"Patient with ID {request.PatientId} not found");

        var order = LabOrder.Create(
            request.PatientId,
            request.OrderedByUserId,
            request.OrderType,
            request.CycleId,
            request.Notes);

        try
        {
            await _labOrderRepo.AddAsync(order, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            foreach (var testInput in request.Tests)
            {
                var test = LabTest.Create(order.Id, testInput.TestCode, testInput.TestName, testInput.ReferenceRange);
                order.AddTest(test);
            }

            await _labOrderRepo.UpdateAsync(order, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            return Result<LabOrderDto>.Failure($"Failed to create lab order: {ex.Message}");
        }

        var saved = await _labOrderRepo.GetByIdWithTestsAsync(order.Id, ct);
        return Result<LabOrderDto>.Success(LabOrderDto.FromEntity(saved!));
    }
}

// ==================== COLLECT SAMPLE ====================
[RequiresFeature(FeatureCodes.Lab)]
public record CollectLabSampleCommand(Guid OrderId) : IRequest<Result<LabOrderDto>>;

public class CollectLabSampleHandler : IRequestHandler<CollectLabSampleCommand, Result<LabOrderDto>>
{
    private readonly ILabOrderRepository _labOrderRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CollectLabSampleHandler(ILabOrderRepository labOrderRepo, IUnitOfWork unitOfWork)
    {
        _labOrderRepo = labOrderRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<LabOrderDto>> Handle(CollectLabSampleCommand request, CancellationToken ct)
    {
        var order = await _labOrderRepo.GetByIdWithTestsAsync(request.OrderId, ct);
        if (order == null)
            return Result<LabOrderDto>.Failure("Lab order not found");

        if (order.Status != "Ordered")
            return Result<LabOrderDto>.Failure($"Cannot collect sample for order with status '{order.Status}'");

        order.CollectSample();
        await _labOrderRepo.UpdateAsync(order, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<LabOrderDto>.Success(LabOrderDto.FromEntity(order));
    }
}

// ==================== ENTER LAB RESULT ====================
public record LabTestResultInput(Guid TestId, string ResultValue, string? ResultUnit, bool IsAbnormal, string? Notes = null);

[RequiresFeature(FeatureCodes.Lab)]
public record EnterLabResultCommand(
    Guid OrderId,
    Guid PerformedByUserId,
    List<LabTestResultInput> Results
) : IRequest<Result<LabOrderDto>>;

public class EnterLabResultValidator : AbstractValidator<EnterLabResultCommand>
{
    public EnterLabResultValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.PerformedByUserId).NotEmpty();
        RuleFor(x => x.Results).NotEmpty().WithMessage("At least one result is required");
        RuleForEach(x => x.Results).ChildRules(r =>
        {
            r.RuleFor(i => i.TestId).NotEmpty();
            r.RuleFor(i => i.ResultValue).NotEmpty().WithMessage("Result value is required");
        });
    }
}

public class EnterLabResultHandler : IRequestHandler<EnterLabResultCommand, Result<LabOrderDto>>
{
    private readonly ILabOrderRepository _labOrderRepo;
    private readonly IUnitOfWork _unitOfWork;

    public EnterLabResultHandler(ILabOrderRepository labOrderRepo, IUnitOfWork unitOfWork)
    {
        _labOrderRepo = labOrderRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<LabOrderDto>> Handle(EnterLabResultCommand request, CancellationToken ct)
    {
        var order = await _labOrderRepo.GetByIdWithTestsAsync(request.OrderId, ct);
        if (order == null)
            return Result<LabOrderDto>.Failure("Lab order not found");

        if (order.Status is "Delivered" or "Completed")
            return Result<LabOrderDto>.Failure($"Cannot enter results for order with status '{order.Status}'");

        foreach (var resultInput in request.Results)
        {
            var test = order.Tests.FirstOrDefault(t => t.Id == resultInput.TestId);
            if (test == null)
                return Result<LabOrderDto>.Failure($"Test with ID {resultInput.TestId} not found in this order");

            test.RecordResult(resultInput.ResultValue, resultInput.ResultUnit, resultInput.IsAbnormal, request.PerformedByUserId, resultInput.Notes);
        }

        // Auto-complete if all tests have results
        if (order.Tests.All(t => t.CompletedAt.HasValue))
            order.Complete();
        else
            order.StartProcessing();

        await _labOrderRepo.UpdateAsync(order, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<LabOrderDto>.Success(LabOrderDto.FromEntity(order));
    }
}

// ==================== DELIVER RESULTS ====================
[RequiresFeature(FeatureCodes.Lab)]
public record DeliverLabResultCommand(Guid OrderId, Guid DeliveredByUserId, string DeliveredTo) : IRequest<Result<LabOrderDto>>;

public class DeliverLabResultHandler : IRequestHandler<DeliverLabResultCommand, Result<LabOrderDto>>
{
    private readonly ILabOrderRepository _labOrderRepo;
    private readonly IUnitOfWork _unitOfWork;

    public DeliverLabResultHandler(ILabOrderRepository labOrderRepo, IUnitOfWork unitOfWork)
    {
        _labOrderRepo = labOrderRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<LabOrderDto>> Handle(DeliverLabResultCommand request, CancellationToken ct)
    {
        var order = await _labOrderRepo.GetByIdWithTestsAsync(request.OrderId, ct);
        if (order == null)
            return Result<LabOrderDto>.Failure("Lab order not found");

        if (order.Status != "Completed")
            return Result<LabOrderDto>.Failure($"Cannot deliver results for order with status '{order.Status}'. Must be Completed first.");

        order.Deliver(request.DeliveredByUserId, request.DeliveredTo);
        await _labOrderRepo.UpdateAsync(order, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<LabOrderDto>.Success(LabOrderDto.FromEntity(order));
    }
}
