using Microsoft.JSInterop;

namespace Castlink.Client.Tests.Fakes;

/// <summary>Hand-rolled <see cref="IJSRuntime"/> double — no bUnit dependency, matching this
/// project's "avoid pulling in an extra test package" ethos (see <c>FixedTimeProvider</c> for the
/// same reasoning elsewhere). <c>InvokeVoidAsync</c>/the params-args <c>InvokeAsync</c> overload
/// used by <c>PlayerTokenService</c> are extension methods that funnel through the two interface
/// methods below, so implementing just these two is enough to intercept every call.</summary>
internal sealed class FakeJSRuntime : IJSRuntime
{
    public List<(string Identifier, object?[]? Args)> Calls { get; } = [];

    public object? NextReturnValue { get; set; }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        Calls.Add((identifier, args));
        return ValueTask.FromResult((TValue)NextReturnValue!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        Calls.Add((identifier, args));
        return ValueTask.FromResult((TValue)NextReturnValue!);
    }
}
