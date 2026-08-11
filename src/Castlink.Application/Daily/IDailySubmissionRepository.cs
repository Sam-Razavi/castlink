using Castlink.Domain;

namespace Castlink.Application.Daily;

/// <summary>Thrown when a submission's insert violates the (date, player_id) unique index — the
/// real enforcement of "one graded attempt per day" lives in that DB constraint, not in
/// application-level logic, so this just gives callers a typed exception instead of parsing a raw
/// <see cref="Microsoft.EntityFrameworkCore.DbUpdateException"/>.</summary>
public sealed class DuplicateSubmissionException : Exception
{
    public DuplicateSubmissionException(string message)
        : base(message)
    {
    }
}

public interface IDailySubmissionRepository
{
    Task InsertAsync(DailySubmission submission, CancellationToken cancellationToken);
}
