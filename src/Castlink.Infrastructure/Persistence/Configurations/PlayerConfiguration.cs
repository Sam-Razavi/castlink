using Castlink.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Castlink.Infrastructure.Persistence.Configurations;

public sealed class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.ToTable("players");

        builder.HasKey(p => p.Id);
        // The id is minted in code (Guid.NewGuid()) when POST /api/players runs, not by the
        // database, so change tracking must not expect the DB to generate it.
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(p => p.DisplayName).HasColumnName("display_name").HasMaxLength(100).IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
    }
}
