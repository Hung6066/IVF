using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Background service that drains the WAF event channel and batch-inserts to DB.
/// Flushes every 2 seconds or when 100 events are buffered, whichever comes first.
/// </summary>
public class WafEventWriter : BackgroundService
{
    private readonly WafEventChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WafEventWriter> _logger;

    private const int BatchSize = 100;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(2);

    public WafEventWriter(
        WafEventChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<WafEventWriter> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WAF event writer started");

        var buffer = new List<WafEventData>(BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(FlushInterval);

                try
                {
                    while (buffer.Count < BatchSize)
                    {
                        var data = await _channel.Reader.ReadAsync(cts.Token);
                        buffer.Add(data);
                    }
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // Flush timer expired — flush whatever we have
                }

                if (buffer.Count > 0)
                {
                    await FlushAsync(buffer, stoppingToken);
                    buffer.Clear();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WAF event writer error, dropping {Count} events", buffer.Count);
                buffer.Clear();
            }
        }

        // Final flush on shutdown
        if (buffer.Count > 0)
        {
            await FlushAsync(buffer, CancellationToken.None);
        }

        _logger.LogInformation("WAF event writer stopped");
    }

    private async Task FlushAsync(List<WafEventData> batch, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

            foreach (var data in batch)
            {
                var entity = WafEvent.Create(
                    data.WafRuleId, data.RuleName, data.RuleGroup, data.Action,
                    data.ClientIp, data.Country, data.RequestPath, data.RequestMethod,
                    data.QueryString, data.UserAgent, data.MatchedPattern, data.MatchedValue,
                    data.ResponseStatusCode, data.Headers, data.CorrelationId, data.ProcessingTimeMs);

                db.WafEvents.Add(entity);
            }

            await db.SaveChangesAsync(ct);
            _logger.LogDebug("Flushed {Count} WAF events to database", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush {Count} WAF events to database", batch.Count);
        }
    }
}
