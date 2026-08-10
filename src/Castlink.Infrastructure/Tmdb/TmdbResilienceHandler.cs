using Castlink.Infrastructure.Configuration;
using Polly;

namespace Castlink.Infrastructure.Tmdb;

/// <summary>
/// A <see cref="DelegatingHandler"/> that wraps every outbound TMDB request in the resilience
/// pipeline from <see cref="TmdbResiliencePipeline"/>. Registered via
/// <c>IHttpClientBuilder.AddHttpMessageHandler</c> rather than the higher-level
/// <c>AddResilienceHandler</c> sugar — same effect, but built directly on the well-established
/// <c>DelegatingHandler</c> + Polly <c>ExecuteAsync</c> pattern, which also makes it trivial to
/// unit test standalone (swap in a fake <see cref="InnerHandler"/>).
/// </summary>
internal sealed class TmdbResilienceHandler : DelegatingHandler
{
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

    public TmdbResilienceHandler(TmdbOptions options)
    {
        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();
        TmdbResiliencePipeline.Configure(builder, options);
        _pipeline = builder.Build();
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        _pipeline.ExecuteAsync(async ct => await base.SendAsync(request, ct), cancellationToken).AsTask();
}
