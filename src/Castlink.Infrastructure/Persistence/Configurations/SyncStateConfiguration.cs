using Castlink.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Castlink.Infrastructure.Persistence.Configurations;

public sealed class SyncStateConfiguration : IEntityTypeConfiguration<SyncState>
{
    public void Configure(EntityTypeBuilder<SyncState> builder)
    {
        builder.ToTable("sync_state");

        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key).HasColumnName("key").HasMaxLength(200);

        builder.Property(s => s.Value).HasColumnName("value").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
    }
}
