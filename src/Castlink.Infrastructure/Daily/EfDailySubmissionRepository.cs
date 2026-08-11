using Castlink.Application.Daily;
using Castlink.Domain;
using Castlink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Castlink.Infrastructure.Daily;

internal sealed class EfDailySubmissionRepository : IDailySubmissionRepository
{
    private readonly CastlinkDbContext _dbContext;

    public EfDailySubmissionRepository(CastlinkDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task InsertAsync(DailySubmission submission, CancellationToken cancellationToken)
    {
        _dbContext.DailySubmissions.Add(submission);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new DuplicateSubmissionException(
                $"Player {submission.PlayerId} has already submitted for {submission.Date:yyyy-MM-dd}.");
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
