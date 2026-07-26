using Microsoft.EntityFrameworkCore;
using Paages.Domain.Entities;

namespace Paages.Infrastructure.Data;

public class PaagesDbContext : DbContext
{
    public PaagesDbContext(DbContextOptions<PaagesDbContext> options) : base(options) { }

    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Folder> Folders => Set<Folder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Note>()
            .HasOne(n => n.Folder)
            .WithMany(f => f.Notes)
            .HasForeignKey(n => n.FolderId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<Folder>()
            .HasOne(f => f.Parent)
            .WithMany(f => f.Children)
            .HasForeignKey(f => f.ParentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}