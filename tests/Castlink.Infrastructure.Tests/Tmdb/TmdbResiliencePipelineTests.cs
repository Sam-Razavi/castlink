using System.Net;
using Castlink.Infrastructure.Configuration;
using Castlink.Infrastructure.Tmdb;
using Polly;
using Xunit;

namespace Castlink.Infrastructure.Tests.Tmdb;

public sealed class TmdbResiliencePipelineTests
{
    [Fact]
    public void CreateRequestRateLimiter_honours_its_configured_requests_per_second_budget()
    {
        var options = new TmdbOptions { RequestsPerSecond = 3 };
        using var limiter = TmdbResiliencePipeline.CreateRequestRateLimiter(options);

        var acquired = new List<bool>();
        for (var i = 0; i < 4; i++)
        {
            using var lease = limiter.AttemptAcquire(1);
            acquired.Add(lease.IsAcquired);
        }

        // The bucket starts full at TokenLimit and hasn't had time to replenish (ReplenishmentPeriod
        // is 1s) — the 4th synchronous attempt within the same instant must be rejected.
        Assert.Equal([true, true, true, false], acquired);
    }

    [Fact]
    public async Task Configure_caps_concurrent_in_flight_calls_at_the_configured_limit()
    {
        // Rate limiter budget set far above what this test could exhaust, so only the
        // concurrency limiter is under test here.
        var options = new TmdbOptions { RequestsPerSecond = 1000, MaxConcurrentConnections = 2 };
        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();
        TmdbResiliencePipeline.Configure(builder, options);
        var pipeline = builder.Build();

        var currentConcurrency = 0;
        var maxObservedConcurrency = 0;
        var gate = new object();

        async ValueTask<HttpResponseMessage> SimulateSlowCallAsync(CancellationToken cancellationToken)
        {
            lock (gate)
            {
                currentConcurrency++;
                maxObservedConcurrency = Math.Max(maxObservedConcurrency, currentConcurrency);
            }

            await Task.Delay(50, cancellationToken);

            lock (gate)
            {
                currentConcurrency--;
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => pipeline.ExecuteAsync(ct => SimulateSlowCallAsync(ct)).AsTask());
        await Task.WhenAll(tasks);

        Assert.True(maxObservedConcurrency <= 2, $"expected max concurrency <= 2, observed {maxObservedConcurrency}");
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.OK, false)]
    [InlineData(HttpStatusCode.NotFound, false)]
    public void IsTransientFailure_retries_only_429_and_5xx(HttpStatusCode status, bool expectRetry)
    {
        using var response = new HttpResponseMessage(status);

        Assert.Equal(expectRetry, TmdbResiliencePipeline.IsTransientFailure(response));
    }
}
