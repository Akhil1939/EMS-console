using ManageEmployee.Models;
using Microsoft.EntityFrameworkCore;

namespace ManageEmployee.Context;
public class AppDbContext : DbContext
{
    public DbSet<Employee> Employees { get; set; }
    public DbSet<EmployeeSalary> EmployeeSalaries { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=employee.db");
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>()
        .HasIndex(e => e.SSN)
        .IsUnique();

        modelBuilder.Entity<EmployeeSalary>()
            .HasOne(es => es.Employee)
            .WithMany()
            .HasForeignKey(es => es.EmployeeId).HasConstraintName("FK_EmployeeSalary_Employee");
    }
}
