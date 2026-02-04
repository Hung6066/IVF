using System.Security.Claims;
using IVF.API.Contracts;
using IVF.Application.Features.Billing.Commands;
using IVF.Application.Features.Billing.Queries;
using MediatR;

namespace IVF.API.Endpoints;

public static class BillingEndpoints
{
    public static void MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/billing").WithTags("Billing").RequireAuthorization("BillingAccess");

        group.MapGet("/invoices", async (IMediator m, string? q, int page = 1, int pageSize = 20) =>
            Results.Ok(await m.Send(new SearchInvoicesQuery(q, page, pageSize))));

        group.MapGet("/invoices/{id:guid}", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new GetInvoiceByIdQuery(id));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapGet("/invoices/patient/{patientId:guid}", async (Guid patientId, IMediator m) =>
            Results.Ok(await m.Send(new GetInvoicesByPatientQuery(patientId))));

        group.MapPost("/invoices", async (CreateInvoiceCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/billing/invoices/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPost("/invoices/{id:guid}/items", async (Guid id, AddItemRequest req, IMediator m) =>
        {
            var r = await m.Send(new AddInvoiceItemCommand(id, req.ServiceCode, req.Description, req.Quantity, req.UnitPrice));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPost("/invoices/{id:guid}/issue", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new IssueInvoiceCommand(id));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPost("/invoices/{id:guid}/pay", async (Guid id, ClaimsPrincipal principal, RecordPaymentRequest req, IMediator m) =>
        {
            var userId = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var r = await m.Send(new RecordPaymentCommand(id, req.Amount, req.PaymentMethod, req.TransactionReference, userId));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });
    }
}
