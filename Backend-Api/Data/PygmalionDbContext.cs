using Backend_Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend_Api.Data;

public sealed class PygmalionDbContext(DbContextOptions<PygmalionDbContext> options) : DbContext(options)
{
    public DbSet<DataGenerated> DataGenerated => Set<DataGenerated>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var generated = modelBuilder.Entity<DataGenerated>();
        generated.ToTable("data_generated");
        generated.HasKey(item => item.Id);
        generated.Property(item => item.Metadata).HasColumnType("jsonb").IsRequired();
        generated.Property(item => item.DateCreated).IsRequired();
        generated.Property(item => item.FilePath).HasMaxLength(2048).IsRequired();
        generated.Property(item => item.CodeHash).HasMaxLength(64).IsRequired();
        generated.HasIndex(item => item.CodeHash).IsUnique();
    }
}
