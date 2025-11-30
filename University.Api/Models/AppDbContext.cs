using Microsoft.EntityFrameworkCore;

namespace University.Api.Models;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Tuition> Tuitions => Set<Tuition>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>()
            .HasIndex(s => s.StudentNo)
            .IsUnique();
    }
}
