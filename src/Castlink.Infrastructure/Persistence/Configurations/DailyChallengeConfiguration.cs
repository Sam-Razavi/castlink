using Castlink.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Castlink.Infrastructure.Persistence.Configurations;

public sealed class DailyChallengeConfiguration : IEntityTypeConfiguration<DailyChallenge>
{
    public void Configure(EntityTypeBuilder<DailyChallenge> builder)
    {
        builder.ToTable("daily_challenges");

        builder.HasKey(c => c.Date);
        builder.Property(c => c.Date).HasColumnName("date").ValueGeneratedNever();

        builder.Property(c => c.FromPersonId).HasColumnName("from_person_id");
        builder.Property(c => c.ToPersonId).HasColumnName("to_person_id");
        builder.Property(c => c.OptimalLength).HasColumnName("optimal_length");
        builder.Property(c => c.GeneratedAt).HasColumnName("generated_at");

        builder.Property(c => c.CanonicalPath)
            .HasColumnName("canonical_path")
            .HasColumnType("jsonb")
            .HasConversion(JsonColumnConverters.PathLinkListConverter)
            .Metadata.SetValueComparer(JsonColumnConverters.PathLinkListComparer);

        // Restrict, not Cascade (contrast Credit's FKs): a daily challenge is an immutable
        // historical record of "today's puzzle was this pair" — it must not silently vanish if a
        // person row is ever touched, and in practice person rows are only ever upserted, never
        // deleted, so this constraint should never actually fire.
        builder.HasOne<Person>().WithMany().HasForeignKey(c => c.FromPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(c => c.ToPersonId).OnDelete(DeleteBehavior.Restrict);
    }
}
