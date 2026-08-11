using Castlink.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Castlink.Infrastructure.Persistence.Configurations;

public sealed class DailySubmissionConfiguration : IEntityTypeConfiguration<DailySubmission>
{
    public void Configure(EntityTypeBuilder<DailySubmission> builder)
    {
        builder.ToTable("daily_submissions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(s => s.Date).HasColumnName("date");
        builder.Property(s => s.PlayerId).HasColumnName("player_id");
        builder.Property(s => s.PathLength).HasColumnName("path_length");
        builder.Property(s => s.Score).HasColumnName("score");
        builder.Property(s => s.DurationMs).HasColumnName("duration_ms");
        builder.Property(s => s.SubmittedAt).HasColumnName("submitted_at");

        builder.Property(s => s.Path)
            .HasColumnName("path")
            .HasColumnType("jsonb")
            .HasConversion(JsonColumnConverters.PathLinkListConverter)
            .Metadata.SetValueComparer(JsonColumnConverters.PathLinkListComparer);

        builder.HasOne<DailyChallenge>().WithMany().HasForeignKey(s => s.Date).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Player>().WithMany().HasForeignKey(s => s.PlayerId).OnDelete(DeleteBehavior.Restrict);

        // One attempt per player per day — this is both the game rule and the concurrency guard
        // the submit flow relies on: a duplicate submit's INSERT hits this constraint and the
        // controller maps that to 409, rather than needing a separate SELECT-then-INSERT race window.
        builder.HasIndex(s => new { s.Date, s.PlayerId })
            .IsUnique()
            .HasDatabaseName("ux_daily_submissions_date_player");
    }
}
