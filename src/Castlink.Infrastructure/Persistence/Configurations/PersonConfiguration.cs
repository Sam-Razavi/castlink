using Castlink.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Castlink.Infrastructure.Persistence.Configurations;

public sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("people");

        builder.HasKey(p => p.Id);
        // TMDB's own person id is the primary key — never DB-generated (see docs/PLAN.md section 2).
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(500).IsRequired();
        builder.Property(p => p.Popularity).HasColumnName("popularity");
        builder.Property(p => p.ProfilePath).HasColumnName("profile_path").HasMaxLength(500);
        builder.Property(p => p.KnownForDepartment).HasColumnName("known_for_department").HasMaxLength(100);
        builder.Property(p => p.CreditCount).HasColumnName("credit_count");
        builder.Property(p => p.LastSyncedAt).HasColumnName("last_synced_at");

        // Fuzzy actor-name search (Phase 3) — trigram GIN index, not a plain btree.
        builder.HasIndex(p => p.Name)
            .HasDatabaseName("ix_people_name_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
    }
}
