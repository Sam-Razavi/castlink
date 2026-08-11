using Castlink.Client.Services;
using Xunit;

namespace Castlink.Client.Tests.Services;

public sealed class DebouncerTests
{
    // Short real delays rather than a fake time abstraction — System.Threading.Timer doesn't take
    // a TimeProvider until you build one yourself, and that's more machinery than a debounce
    // utility this small warrants. A few tens of milliseconds of real wait keeps this fast enough.
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan SafetyMargin = TimeSpan.FromMilliseconds(100);

    [Fact]
    public async Task Invokes_the_action_once_the_delay_elapses()
    {
        using var debouncer = new Debouncer(DebounceDelay);
        var invoked = false;

        debouncer.Trigger(() =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        await Task.Delay(DebounceDelay + SafetyMargin);

        Assert.True(invoked);
    }

    [Fact]
    public async Task Retriggering_within_the_delay_window_only_runs_the_latest_action()
    {
        using var debouncer = new Debouncer(DebounceDelay);
        var callCount = 0;
        string? lastValue = null;

        debouncer.Trigger(() =>
        {
            callCount++;
            lastValue = "first";
            return Task.CompletedTask;
        });

        // Retrigger well before the first delay would have elapsed.
        await Task.Delay(TimeSpan.FromMilliseconds(10));
        debouncer.Trigger(() =>
        {
            callCount++;
            lastValue = "second";
            return Task.CompletedTask;
        });

        await Task.Delay(DebounceDelay + SafetyMargin);

        Assert.Equal(1, callCount);
        Assert.Equal("second", lastValue);
    }

    [Fact]
    public async Task Does_not_invoke_anything_if_disposed_before_the_delay_elapses()
    {
        var debouncer = new Debouncer(DebounceDelay);
        var invoked = false;

        debouncer.Trigger(() =>
        {
            invoked = true;
            return Task.CompletedTask;
        });
        debouncer.Dispose();

        await Task.Delay(DebounceDelay + SafetyMargin);

        Assert.False(invoked);
    }
}
