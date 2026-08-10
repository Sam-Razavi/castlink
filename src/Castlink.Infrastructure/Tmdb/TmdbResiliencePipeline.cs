using System.Net;
using System.Threading.RateLimiting;
using Castlink.Infrastructure.Configuration;
using Polly;
using Polly.Retry;

namespace Castlink.Infrastructure.Tmdb;

/// <summary>
/// Builds the resilience strategy for calls to TMDB: a token-bucket rate limiter (TMDB's CDN caps
/// at ~50 req/s per IP; we stay well under it), a concurrency cap, and jittered exponential
/// backoff on 429/5xx. See docs/PLAN.md section 4 for the constraints this is built around.
/// Split into named factory methods so each piece is independently unit testable without needing
/// a full HTTP round trip.
/// </summary>
internal static class TmdbResiliencePipeline
{
    public static void Configure(ResiliencePipelineBuilder<HttpResponseMessage> builder, TmdbOptions options)
    {
        builder
            .AddRateLimiter(CreateRequestRateLimiter(options))
            .AddConcurrencyLimiter(options.MaxConcurrentConnections, queueLimit: options.MaxConcurrentConnections * 4)
            .AddRetry(CreateRetryOptions());
    }

    internal static TokenBucketRateLimiter CreateRequestRateLimiter(TmdbOptions options) =>
        new(new TokenBucketRateLimiterOptions
        {
            TokenLimit = options.RequestsPerSecond,
            TokensPerPeriod = options.RequestsPerSecond,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = int.MaxValue,
            AutoReplenishment = true,
        });

    internal static RetryStrategyOptions<HttpResponseMessage> CreateRetryOptions() => new()
    {
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .HandleResult(IsTransientFailure),
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        MaxRetryAttempts = 5,
        Delay = TimeSpan.FromSeconds(1),
    };

    internal static bool IsTransientFailure(HttpResponseMessage response) =>
        response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
}
