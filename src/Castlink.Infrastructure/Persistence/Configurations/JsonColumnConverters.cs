using System.Text.Json;
using Castlink.Domain;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Castlink.Infrastructure.Persistence.Configurations;

/// <summary>
/// Shared EF Core value conversion for the <c>jsonb</c> path columns on
/// <see cref="DailyChallenge.CanonicalPath"/> and <see cref="DailySubmission.Path"/> — both store a
/// <see cref="List{PathLinkRecord}"/> as JSON rather than a normalised child table, since a path is
/// always read/written whole and never queried into (see docs/PLAN.md Phase 4).
/// </summary>
internal static class JsonColumnConverters
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static readonly ValueConverter<List<PathLinkRecord>, string> PathLinkListConverter = new(
        value => JsonSerializer.Serialize(value, SerializerOptions),
        json => JsonSerializer.Deserialize<List<PathLinkRecord>>(json, SerializerOptions) ?? new List<PathLinkRecord>());

    // EF Core can't tell two deserialized List<T> instances apart by reference, so change tracking
    // needs an explicit structural comparer here or it will think every row changed on every save.
    public static readonly ValueComparer<List<PathLinkRecord>> PathLinkListComparer = new(
        (left, right) => (left ?? new List<PathLinkRecord>()).SequenceEqual(right ?? new List<PathLinkRecord>()),
        value => value.Aggregate(0, (hash, link) => HashCode.Combine(hash, link)),
        value => value.ToList());
}
