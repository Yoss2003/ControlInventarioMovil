using Microsoft.EntityFrameworkCore;
using ControlInventario.Shared.Models;

namespace ControlInventarioMovil.Data
{
    public class LocalDbContext : DbContext
    {
        // ==========================================
        // TABLAS UNIVERSALES (Se descargan al 100%)
        // ==========================================
        public DbSet<Currency> Currencies { get; set; } = null!;
        public DbSet<DateFormat> DateFormats { get; set; } = null!;
        public DbSet<Theme> Themes { get; set; } = null!;
        public DbSet<TimeZoneItem> TimeZoneItems { get; set; } = null!;
        public DbSet<Language> Languages { get; set; } = null!;
        public DbSet<Parameters> Parameters { get; set; } = null!;
        public DbSet<Permission> Permissions { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<RolePermission> RolePermissions { get; set; } = null!;
        public DbSet<ActionItem> ActionItems { get; set; } = null!;
        public DbSet<SalesMode> SalesModes { get; set; } = null!;
        public DbSet<MeasurementUnit> MeasurementUnits { get; set; } = null!;
        public DbSet<CategoryMeasurementUnit> CategoryMeasurementUnits { get; set; } = null!;
        public DbSet<ExchangeRate> ExchangeRates { get; set; } = null!;

        // ==========================================
        // TABLAS AISLADAS (Se filtrarán por Usuario/Empresa)
        // ==========================================
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Profile> Profiles { get; set; } = null!;
        public DbSet<Inventory> Inventories { get; set; } = null!;
        public DbSet<SharedInventory> SharedInventories { get; set; } = null!;
        public DbSet<Employee> Employees { get; set; } = null!;
        public DbSet<Customer> Customer { get; set; } = null!;
        public DbSet<Supplier> Supplier { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Brand> Brands { get; set; } = null!;
        public DbSet<Article> Articles { get; set; } = null!;
        public DbSet<Movement> Movements { get; set; } = null!;
        public DbSet<HistoryLog> HistoryLogs { get; set; } = null!;
        public DbSet<Sale> Sales { get; set; } = null!;
        public DbSet<SaleDetail> SaleDetails { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<Company> Companies { get; set; }

        // Tablas adicionales financieras (opcionales)
        public DbSet<AccountReceivable> AccountReceivables { get; set; } = null!;
        public DbSet<InstallmentPayment> InstallmentPayments { get; set; } = null!;

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