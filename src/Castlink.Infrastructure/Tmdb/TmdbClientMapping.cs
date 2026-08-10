using System.Globalization;
using Castlink.Application.Ingestion;
using Castlink.Infrastructure.Tmdb.Dto;

namespace Castlink.Infrastructure.Tmdb;

/// <summary>Pure DTO -&gt; Application-record mapping, isolated from HTTP so it's directly unit testable.</summary>
internal static class TmdbClientMapping
{
    public static MovieIngestionRecord ToIngestionRecord(TmdbMovieDetailResponse response)
    {
        var cast = response.Credits?.Cast ?? [];

        return new MovieIngestionRecord(
            FilmId: response.Id,
            Title: response.Title,
            ReleaseYear: ParseReleaseYear(response.ReleaseDate),
            Popularity: response.Popularity,
            VoteCount: response.VoteCount,
            PosterPath: response.PosterPath,
            Cast: [.. cast.Select(ToCastCreditRecord)]);
    }

    private static CastCreditRecord ToCastCreditRecord(TmdbCastMember member) =>
        new(
            PersonId: member.Id,
            Name: member.Name,
            Popularity: member.Popularity,
            ProfilePath: member.ProfilePath,
            KnownForDepartment: member.KnownForDepartment,
            BillingOrder: member.Order,
            Character: member.Character);

    /// <summary>
    /// TMDB's <c>release_date</c> is <c>"YYYY-MM-DD"</c>, or an empty string for unreleased/undated
    /// films — never treat an unparsable value as a hard error, just leave the year unknown.
    /// </summary>
    private static int? ParseReleaseYear(string? releaseDate) =>
        !string.IsNullOrWhiteSpace(releaseDate)
        && DateOnly.TryParseExact(releaseDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.Year
            : null;
}
