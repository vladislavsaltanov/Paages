using Microsoft.EntityFrameworkCore;
using Paages.Domain.Entities;

namespace Paages.Infrastructure.Data;

public class PaagesDbContext : DbContext
{
    public PaagesDbContext(DbContextOptions<PaagesDbContext> options) : base(options) { }

    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Folder> Folders => Set<Folder>();
}