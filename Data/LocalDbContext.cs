using Microsoft.EntityFrameworkCore;
using ControlInventario.Shared.Models;

namespace ControlInventarioMovil.Data
{
    public class LocalDbContext : DbContext
    {
        // Catálogos y Entidades Principales
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Employee> Employees { get; set; } = null!;
        public DbSet<Inventory> Inventories { get; set; } = null!;
        public DbSet<Article> Articles { get; set; } = null!;
        public DbSet<Brand> Brands { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Customer> Customer { get; set; } = null!;
        public DbSet<Supplier> Supplier { get; set; } = null!;
        public DbSet<Currency> Currencies { get; set; } = null!;
        public DbSet<Movement> Movements { get; set; } = null!;
        public DbSet<Sale> Sales { get; set; } = null!;
        public DbSet<AccountReceivable> AccountReceivables { get; set; } = null!;
        public DbSet<InstallmentPayment> InstallmentPayments { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<Permission> Permissions { get; set; } = null!;
        public DbSet<Profile> Profiles { get; set; } = null!;
        public DbSet<Parameters> Parameters { get; set; } = null!;
        public DbSet<HistoryLog> HistoryLogs { get; set; } = null!;

        private readonly string _dbPath;

        public LocalDbContext()
        {
            var basePath = FileSystem.AppDataDirectory;
            _dbPath = Path.Combine(basePath, "inventory_local.db3");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Filename={_dbPath}");
        }
    }
}