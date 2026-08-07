using Microsoft.EntityFrameworkCore;
using Paages.Domain.Entities;

namespace Paages.Infrastructure.Data;

public class PaagesDbContext : DbContext
{
    public PaagesDbContext(DbContextOptions<PaagesDbContext> options) : base(options) { }

    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

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

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(t => t.TokenHash)
            .IsUnique();

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(t => t.FamilyId);

        modelBuilder.Entity<RefreshToken>()
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Note>()
            .HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Note>()
            .HasIndex(n => n.UserId);

        modelBuilder.Entity<Folder>()
            .HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Folder>()
            .HasIndex(f => f.UserId);
    }
}