namespace Castlink.Client.Services;

/// <summary>
/// Delays invoking a callback until this many milliseconds have passed without a new
/// <see cref="Trigger"/> call — what a search-as-you-type box needs so it doesn't fire a request
/// on every keystroke. Deliberately plain <see cref="Timer"/>, no Blazor dependency, so it's
/// independently unit testable with no browser and no component host.
///
/// Blazor-specific note (this is the one place it matters): the callback runs on a thread-pool
/// timer thread, not Blazor's synchronization context — a caller that touches component state from
/// inside the callback must marshal back via <c>InvokeAsync</c>, or the UI won't update. See
/// <c>ActorSearchBox.razor</c> for where that happens.
/// </summary>
public sealed class Debouncer : IDisposable
{
    private readonly TimeSpan _delay;
    private readonly Timer _timer;
    private Func<Task>? _pendingAction;

    public Debouncer(TimeSpan delay)
    {
        _delay = delay;
        _timer = new Timer(OnElapsed);
    }

    public void Trigger(Func<Task> action)
    {
        _pendingAction = action;
        _timer.Change(_delay, Timeout.InfiniteTimeSpan);
    }

    private void OnElapsed(object? state)
    {
        var action = _pendingAction;
        _pendingAction = null;

        if (action is not null)
        {
            _ = action();
        }
    }

    public void Dispose() => _timer.Dispose();
}
