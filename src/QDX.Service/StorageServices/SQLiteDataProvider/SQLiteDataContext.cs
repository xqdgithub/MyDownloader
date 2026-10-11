using Microsoft.EntityFrameworkCore;
using QDX.Serivices.StorageServices.SQLiteDataProvider.Entities;

namespace QDX.Serivices.StorageServices.DBDataProvider;

public partial class SQLiteDataContext : DbContext
{
    
    public string ConnectionString { get; set; } = "Data Source=default.db";
    public SQLiteDataContext()
    {
    }

    public SQLiteDataContext(DbContextOptions<SQLiteDataContext> options)
        : base(options)
    {

    }

    public virtual DbSet<DataItem> DataItems { get; set; }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlite(ConnectionString);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<DataItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("data_item");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Type)
                .HasColumnName("type");
            entity.Property(e => e.Key)
            .HasColumnType("text")
            .HasColumnName("key");
            entity.Property(e => e.Value)
                .HasColumnType("text")
                .HasColumnName("value");
            entity.HasIndex(e => new { e.Type, e.Key, }, "keyIndex");
        });
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
