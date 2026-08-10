using System.Text.Json;
using Castlink.Infrastructure.Tmdb;
using Castlink.Infrastructure.Tmdb.Dto;
using Xunit;

namespace Castlink.Infrastructure.Tests.Tmdb;

public sealed class TmdbClientMappingTests
{
    // Recorded (trimmed, fictionalised) shape of a real `/movie/{id}?append_to_response=credits`
    // response — this is the fixture the mapping is tested against.
    private const string MovieWithCreditsJson = """
        {
          "id": 27205,
          "title": "A Dream Within a Dream",
          "release_date": "2010-07-16",
          "popularity": 83.4,
          "vote_count": 34521,
          "poster_path": "/poster.jpg",
          "credits": {
            "cast": [
              { "id": 6193, "name": "Leading Actor", "popularity": 45.2, "profile_path": "/lead.jpg", "known_for_department": "Acting", "order": 0, "character": "Protagonist" },
              { "id": 24045, "name": "Supporting Actor", "popularity": 12.1, "profile_path": null, "known_for_department": "Acting", "order": 1, "character": "Sidekick" }
            ]
          }
        }
        """;

    private const string MovieWithoutCreditsJson = """
        {
          "id": 99,
          "title": "Undated Film",
          "release_date": "",
          "popularity": 1.2,
          "vote_count": 0,
          "poster_path": null
        }
        """;

    [Fact]
    public void ToIngestionRecord_maps_film_fields_and_full_cast()
    {
        var dto = JsonSerializer.Deserialize<TmdbMovieDetailResponse>(MovieWithCreditsJson, TmdbJsonOptions.Default)!;

        var record = TmdbClientMapping.ToIngestionRecord(dto);

        Assert.Equal(27205, record.FilmId);
        Assert.Equal("A Dream Within a Dream", record.Title);
        Assert.Equal(2010, record.ReleaseYear);
        Assert.Equal(83.4, record.Popularity);
        Assert.Equal(34521, record.VoteCount);
        Assert.Equal("/poster.jpg", record.PosterPath);

        Assert.Equal(2, record.Cast.Count);
        var lead = record.Cast[0];
        Assert.Equal(6193, lead.PersonId);
        Assert.Equal("Leading Actor", lead.Name);
        Assert.Equal(45.2, lead.Popularity);
        Assert.Equal("/lead.jpg", lead.ProfilePath);
        Assert.Equal("Acting", lead.KnownForDepartment);
        Assert.Equal(0, lead.BillingOrder);
        Assert.Equal("Protagonist", lead.Character);

        var supporting = record.Cast[1];
        Assert.Null(supporting.ProfilePath);
        Assert.Equal(1, supporting.BillingOrder);
    }

    [Fact]
    public void ToIngestionRecord_treats_missing_credits_as_an_empty_cast_not_an_error()
    {
        var dto = JsonSerializer.Deserialize<TmdbMovieDetailResponse>(MovieWithoutCreditsJson, TmdbJsonOptions.Default)!;

        var record = TmdbClientMapping.ToIngestionRecord(dto);

        Assert.Empty(record.Cast);
    }

    [Fact]
    public void ToIngestionRecord_treats_an_empty_release_date_as_an_unknown_year()
    {
        var dto = JsonSerializer.Deserialize<TmdbMovieDetailResponse>(MovieWithoutCreditsJson, TmdbJsonOptions.Default)!;

        var record = TmdbClientMapping.ToIngestionRecord(dto);

        Assert.Null(record.ReleaseYear);
    }
}
