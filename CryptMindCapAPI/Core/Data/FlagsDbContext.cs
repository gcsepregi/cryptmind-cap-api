using Microsoft.EntityFrameworkCore;

namespace CryptMindCapAPI.Core.Data;

public class FlagsDbContext(DbContextOptions<FlagsDbContext> options) : DbContext(options)
{
    public DbSet<FeatureOverride> Overrides => Set<FeatureOverride>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FeatureOverride>(e =>
        {
            e.ToTable("overrides");
            e.HasKey(x => new { x.EntitlementId, x.FlagKey });
            e.Property(x => x.EntitlementId).HasColumnName("entitlement_id").HasMaxLength(256);
            e.Property(x => x.FlagKey).HasColumnName("flag_key").HasMaxLength(128);
            e.Property(x => x.Value).HasColumnName("value");
        });
    }
}
