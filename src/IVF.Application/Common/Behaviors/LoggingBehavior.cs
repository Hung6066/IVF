using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IVF.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs every command/query with structured properties:
/// - Request type name, execution duration, success/failure
/// - Performance warnings for slow handlers (>500ms warning, >2000ms error)
/// - Exception details on failure
/// </summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private const int WarningThresholdMs = 500;
    private const int ErrorThresholdMs = 2000;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var requestType = IsCommand(requestName) ? "Command" : "Query";

        _logger.LogDebug("[MediatR] Starting {RequestType} {RequestName}", requestType, requestName);

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next();
            sw.Stop();

            if (sw.ElapsedMilliseconds > ErrorThresholdMs)
            {
                _logger.LogError(
                    "[MediatR] SLOW {RequestType} {RequestName} completed in {ElapsedMs}ms (threshold: {ThresholdMs}ms)",
                    requestType, requestName, sw.ElapsedMilliseconds, ErrorThresholdMs);
            }
            else if (sw.ElapsedMilliseconds > WarningThresholdMs)
            {
                _logger.LogWarning(
                    "[MediatR] SLOW {RequestType} {RequestName} completed in {ElapsedMs}ms (threshold: {ThresholdMs}ms)",
                    requestType, requestName, sw.ElapsedMilliseconds, WarningThresholdMs);
            }
            else
            {
                _logger.LogInformation(
                    "[MediatR] {RequestType} {RequestName} completed in {ElapsedMs}ms",
                    requestType, requestName, sw.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "[MediatR] {RequestType} {RequestName} FAILED after {ElapsedMs}ms — {ErrorType}: {ErrorMessage}",
                requestType, requestName, sw.ElapsedMilliseconds, ex.GetType().Name, ex.Message);
            throw;
        }
    }

    private static bool IsCommand(string name)
        => name.EndsWith("Command", StringComparison.OrdinalIgnoreCase);
}
