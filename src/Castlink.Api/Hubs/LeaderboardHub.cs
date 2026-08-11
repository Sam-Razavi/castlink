using Microsoft.AspNetCore.SignalR;

namespace Castlink.Api.Hubs;

/// <summary>
/// Live leaderboard push (see docs/PLAN.md Phase 4): clients join the group for a given date's
/// challenge, and <c>DailyController.Submit</c> broadcasts <c>LeaderboardUpdated</c> to that group
/// after each accepted submission. No server-to-client auth here — joining a date's group only
/// grants "receive that day's public rankings," nothing sensitive.
/// </summary>
public sealed class LeaderboardHub : Hub
{
    public Task JoinDaily(string date) => Groups.AddToGroupAsync(Context.ConnectionId, GroupName(date));

    internal static string GroupName(string date) => $"daily:{date}";
}
