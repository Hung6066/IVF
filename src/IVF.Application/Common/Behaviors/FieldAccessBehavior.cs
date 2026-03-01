using IVF.Application.Common.Interfaces;
using IVF.Application.Common.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IVF.Application.Common.Behaviors;

/// <summary>
/// Marker interface for queries that return DTOs needing field-level access control.
/// </summary>
public interface IFieldAccessProtected
{
    string TableName { get; }
}

/// <summary>
/// MediatR pipeline behavior that automatically applies field-level access masking
/// to query responses based on the current user's role and configured FieldAccessPolicies.
/// </summary>
public class FieldAccessBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IFieldAccessService _fieldAccessService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<FieldAccessBehavior<TRequest, TResponse>> _logger;

    public FieldAccessBehavior(
        IFieldAccessService fieldAccessService,
        ICurrentUserService currentUser,
        ILogger<FieldAccessBehavior<TRequest, TResponse>> logger)
    {
        _fieldAccessService = fieldAccessService;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next();

        if (request is not IFieldAccessProtected fap || response is null)
            return response;

        var role = _currentUser.Role;
        if (string.IsNullOrEmpty(role) || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return response;

        try
        {
            await ApplyToResponse(response, fap.TableName, role, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply field access for {Table}, role {Role}", fap.TableName, role);
        }

        return response;
    }

    private async Task ApplyToResponse(TResponse response, string tableName, string role, CancellationToken ct)
    {
        // Handle Result<T> — unwrap the Value
        var responseType = response!.GetType();

        if (responseType.IsGenericType)
        {
            var genericDef = responseType.GetGenericTypeDefinition();
            var fullName = genericDef.FullName;

            // Result<T>
            if (fullName?.Contains("Result`1") == true)
            {
                var valueProp = responseType.GetProperty("Value");
                var isSuccessProp = responseType.GetProperty("IsSuccess");

                if (valueProp is not null && isSuccessProp is not null)
                {
                    var isSuccess = isSuccessProp.GetValue(response) as bool?;
                    if (isSuccess != true) return;

                    var value = valueProp.GetValue(response);
                    if (value is null) return;

                    await ApplyToValue(value, tableName, role, ct);
                }
                return;
            }

            // PagedResult<T>
            if (fullName?.Contains("PagedResult`1") == true)
            {
                var itemsProp = responseType.GetProperty("Items");
                if (itemsProp is not null)
                {
                    var items = itemsProp.GetValue(response);
                    if (items is System.Collections.IEnumerable enumerable)
                    {
                        foreach (var item in enumerable)
                        {
                            await ApplyToValue(item, tableName, role, ct);
                        }
                    }
                }
                return;
            }
        }

        // Direct DTO
        await ApplyToValue(response, tableName, role, ct);
    }

    private async Task ApplyToValue(object value, string tableName, string role, CancellationToken ct)
    {
        // Use reflection to call the generic method
        var method = typeof(IFieldAccessService)
            .GetMethod(nameof(IFieldAccessService.ApplyFieldAccessAsync),
                new[] { value.GetType(), typeof(string), typeof(string), typeof(CancellationToken) });

        if (method is null)
        {
            // Fall back to generic method with runtime type
            var genericMethod = typeof(IFieldAccessService)
                .GetMethods()
                .First(m => m.Name == nameof(IFieldAccessService.ApplyFieldAccessAsync)
                         && m.GetParameters().Length == 4
                         && !m.GetParameters()[0].ParameterType.IsGenericType)
                .MakeGenericMethod(value.GetType());

            var task = genericMethod.Invoke(_fieldAccessService, new[] { value, tableName, role, ct });
            if (task is Task t) await t;
        }
    }
}
