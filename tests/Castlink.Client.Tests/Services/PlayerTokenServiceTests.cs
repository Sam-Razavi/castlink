using Castlink.Client.Services;
using Castlink.Client.Tests.Fakes;
using Xunit;

namespace Castlink.Client.Tests.Services;

public sealed class PlayerTokenServiceTests
{
    [Fact]
    public async Task GetTokenAsync_reads_the_expected_localStorage_key_and_returns_the_stored_value()
    {
        var jsRuntime = new FakeJSRuntime { NextReturnValue = "stored-token" };
        var service = new PlayerTokenService(jsRuntime);

        var token = await service.GetTokenAsync();

        Assert.Equal("stored-token", token);
        var call = Assert.Single(jsRuntime.Calls);
        Assert.Equal("localStorage.getItem", call.Identifier);
        Assert.Equal(["castlink.playerToken"], call.Args);
    }

    [Fact]
    public async Task GetTokenAsync_returns_null_when_nothing_is_stored()
    {
        var jsRuntime = new FakeJSRuntime { NextReturnValue = null };
        var service = new PlayerTokenService(jsRuntime);

        var token = await service.GetTokenAsync();

        Assert.Null(token);
    }

    [Fact]
    public async Task SaveTokenAsync_writes_the_expected_localStorage_key_and_value()
    {
        var jsRuntime = new FakeJSRuntime();
        var service = new PlayerTokenService(jsRuntime);

        await service.SaveTokenAsync("new-token");

        var call = Assert.Single(jsRuntime.Calls);
        Assert.Equal("localStorage.setItem", call.Identifier);
        Assert.Equal(["castlink.playerToken", "new-token"], call.Args);
    }
}
