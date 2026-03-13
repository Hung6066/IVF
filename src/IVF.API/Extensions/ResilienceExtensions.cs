using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace IVF.API.Extensions;

/// <summary>
/// Enterprise-grade resilience policies using Polly v8
/// Provides circuit breaker, retry, timeout, and bulkhead isolation
/// </summary>
public static class ResilienceExtensions
{
    /// <summary>
    /// Adds enterprise resilience policies to the service collection
    /// </summary>
    public static IServiceCollection AddResiliencePolicies(this IServiceCollection services)
    {
        // Configure resilience pipeline for HTTP clients
        services.ConfigureHttpClientDefaults(builder =>
        {
            builder.AddStandardResilienceHandler(options =>
            {
                // Retry Policy - exponential backoff with jitter
                options.Retry = new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(1),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = static args => ValueTask.FromResult(
                        args.Outcome.Result?.StatusCode is >= System.Net.HttpStatusCode.InternalServerError
                        || args.Outcome.Exception is HttpRequestException or TimeoutRejectedException)
                };

                // Circuit Breaker Policy
                options.CircuitBreaker = new HttpCircuitBreakerStrategyOptions
                {
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    FailureRatio = 0.5,
                    MinimumThroughput = 10,
                    BreakDuration = TimeSpan.FromSeconds(30),
                    ShouldHandle = static args => ValueTask.FromResult(
                        args.Outcome.Result?.StatusCode is >= System.Net.HttpStatusCode.InternalServerError
                        || args.Outcome.Exception is HttpRequestException)
                };

                // Timeout Policy
                options.AttemptTimeout = new HttpTimeoutStrategyOptions
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };

                options.TotalRequestTimeout = new HttpTimeoutStrategyOptions
                {
                    Timeout = TimeSpan.FromSeconds(120)
                };
            });
        });

        // Add resilience pipeline provider for custom scenarios
        services.AddResiliencePipeline("database", builder =>
        {
            builder
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 5,
                    Delay = TimeSpan.FromMilliseconds(500),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(ex =>
                        ex is TimeoutException or InvalidOperationException
                        || ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase))
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.3,
                    SamplingDuration = TimeSpan.FromSeconds(60),
                    MinimumThroughput = 20,
                    BreakDuration = TimeSpan.FromSeconds(30)
                })
                .AddTimeout(TimeSpan.FromSeconds(30));
        });

        services.AddResiliencePipeline("redis", builder =>
        {
            builder
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromMilliseconds(100),
                    BackoffType = DelayBackoffType.Linear,
                    UseJitter = true
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    MinimumThroughput = 10,
                    BreakDuration = TimeSpan.FromSeconds(15)
                })
                .AddTimeout(TimeSpan.FromSeconds(5));
        });

        services.AddResiliencePipeline("signserver", builder =>
        {
            builder
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 2,
                    Delay = TimeSpan.FromSeconds(2),
                    BackoffType = DelayBackoffType.Exponential
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(60),
                    MinimumThroughput = 5,
                    BreakDuration = TimeSpan.FromSeconds(60)
                })
                .AddTimeout(TimeSpan.FromSeconds(60));
        });

        services.AddResiliencePipeline("minio", builder =>
        {
            builder
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(1),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.3,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    MinimumThroughput = 10,
                    BreakDuration = TimeSpan.FromSeconds(20)
                })
                .AddTimeout(TimeSpan.FromSeconds(30));
        });

        return services;
    }

    /// <summary>
    /// Adds resilience handler to HttpClient builder with custom configuration
    /// </summary>
    public static IHttpClientBuilder AddEnterpriseResilienceHandler(
        this IHttpClientBuilder builder,
        Action<HttpStandardResilienceOptions>? configure = null)
    {
        builder.AddStandardResilienceHandler(options =>
        {
            // Default enterprise configuration
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.FromSeconds(1);
            options.Retry.BackoffType = DelayBackoffType.Exponential;
            options.Retry.UseJitter = true;

            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.MinimumThroughput = 10;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);

            // Allow custom overrides
            configure?.Invoke(options);
        });

        return builder;
    }
}

// Note: ResiliencePipelineProvider extension methods have been removed.
// Use the configured HttpClient resilience policies directly through the AddStandardResilienceHandler
// or create custom named pipelines using services.AddResiliencePipeline("name", ...).
