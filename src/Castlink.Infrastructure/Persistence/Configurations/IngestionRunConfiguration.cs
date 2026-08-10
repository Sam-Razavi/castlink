using Castlink.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Castlink.Infrastructure.Persistence.Configurations;

public sealed class IngestionRunConfiguration : IEntityTypeConfiguration<IngestionRun>
{
    public void Configure(EntityTypeBuilder<IngestionRun> builder)
    {
        builder.ToTable("ingestion_runs");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.StartedAt).HasColumnName("started_at");
        builder.Property(r => r.CompletedAt).HasColumnName("completed_at");
        // Stored as text, not the enum's numeric value — readable straight out of the DB when
        // operators are eyeballing ingestion history.
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.FilmsProcessed).HasColumnName("films_processed");
        builder.Property(r => r.PeopleProcessed).HasColumnName("people_processed");
        builder.Property(r => r.Error).HasColumnName("error");
    }
}
