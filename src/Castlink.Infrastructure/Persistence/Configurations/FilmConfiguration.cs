using Castlink.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Castlink.Infrastructure.Persistence.Configurations;

public sealed class FilmConfiguration : IEntityTypeConfiguration<Film>
{
    public void Configure(EntityTypeBuilder<Film> builder)
    {
        builder.ToTable("films");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(f => f.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
        builder.Property(f => f.ReleaseYear).HasColumnName("release_year");
        builder.Property(f => f.Popularity).HasColumnName("popularity");
        builder.Property(f => f.VoteCount).HasColumnName("vote_count");
        builder.Property(f => f.PosterPath).HasColumnName("poster_path").HasMaxLength(500);
        builder.Property(f => f.LastSyncedAt).HasColumnName("last_synced_at");

        // Supports the daily-challenge candidate-pool query (Phase 4) and general "well-known
        // film" filtering.
        builder.HasIndex(f => f.VoteCount).HasDatabaseName("ix_films_vote_count");
    }
}
