using Castlink.Application.Daily;
using Castlink.Domain;

namespace Castlink.Api.Tests.Fakes;

/// <summary>Enforces the (date, playerId) uniqueness itself, mirroring the real EF implementation's
/// unique-index-backed behaviour.</summary>
internal sealed class FakeDailySubmissionRepository : IDailySubmissionRepository
{
    private readonly Dictionary<(DateOnly, Guid), DailySubmission> _submissions = new();

    public IReadOnlyList<DailySubmission> Submissions => _submissions.Values.ToList();

    public Task InsertAsync(DailySubmission submission, CancellationToken cancellationToken)
    {
        var key = (submission.Date, submission.PlayerId);
        if (!_submissions.TryAdd(key, submission))
        {
            throw new DuplicateSubmissionException($"Player {submission.PlayerId} has already submitted for {submission.Date:yyyy-MM-dd}.");
        }

        return Task.CompletedTask;
    }
}
