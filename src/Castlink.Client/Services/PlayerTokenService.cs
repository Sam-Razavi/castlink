using Microsoft.JSInterop;

namespace Castlink.Client.Services;

/// <summary>
/// Wraps the anonymous player token in the browser's localStorage (see docs/PLAN.md Phase 4:
/// "signed, localStorage"). No custom JS interop file needed — <see cref="IJSRuntime"/> can invoke
/// the browser's built-in <c>localStorage</c> global directly, it isn't limited to app-defined
/// JavaScript.
/// </summary>
public sealed class PlayerTokenService
{
    private const string StorageKey = "castlink.playerToken";

    private readonly IJSRuntime _jsRuntime;

    public PlayerTokenService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public ValueTask<string?> GetTokenAsync() =>
        _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);

    public ValueTask SaveTokenAsync(string token) =>
        _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, token);
}
