using Castlink.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Castlink.Infrastructure.Persistence.Configurations;

public sealed class CreditConfiguration : IEntityTypeConfiguration<Credit>
{
    public void Configure(EntityTypeBuilder<Credit> builder)
    {
        builder.ToTable("credits");

        builder.HasKey(c => new { c.FilmId, c.PersonId });
        builder.Property(c => c.FilmId).HasColumnName("film_id");
        builder.Property(c => c.PersonId).HasColumnName("person_id");
        builder.Property(c => c.BillingOrder).HasColumnName("billing_order");
        builder.Property(c => c.Character).HasColumnName("character").HasMaxLength(500);

        // Deliberately no navigation properties on Credit/Film/Person — Domain stays plain POCOs
        // (see docs/PLAN.md). FK-only relationships still get real constraints and cascading deletes.
        builder.HasOne<Film>().WithMany().HasForeignKey(c => c.FilmId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Person>().WithMany().HasForeignKey(c => c.PersonId).OnDelete(DeleteBehavior.Cascade);

        // The bipartite graph traversal walks both directions. The composite PK (film_id, person_id)
        // already indexes film_id as its leading column, so only person_id needs an explicit index.
        builder.HasIndex(c => c.PersonId).HasDatabaseName("ix_credits_person_id");
    }
}
