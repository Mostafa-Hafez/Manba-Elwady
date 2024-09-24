using ElWadManbaaELWaddidi.Models;
using ManbaaELWaddi.Models;
using Microsoft.EntityFrameworkCore;

namespace ManbaaELWaddi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {

        }


        public DbSet<Client>? Clients { get; set; }
        public DbSet<Inventory>? Inventories { get; set; }
        public DbSet<Invoice>? Invoices { get; set; }
        public DbSet<Order>? Orders { get; set; }
        public DbSet<User>? Users { get; set; }
        public DbSet<Admin>? Admins { get; set; }
        public DbSet<District>? Districts { get; set; }
        public DbSet<CalcQuot>? CalcQuots { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configuring the Orders to Users relationship
            modelBuilder.Entity<Order>()
                .HasOne(o => o.FkUser) // Specify the navigation property
                .WithMany(u => u.Orders) // Specify the inverse navigation property
                .HasForeignKey(o => o.FkUserId) // Specify the foreign key
                .OnDelete(DeleteBehavior.Restrict); // Set the delete behavior to Restrict

            // If you have another relationship that might cause the cascade path, configure it similarly
            modelBuilder.Entity<Client>()
                .HasOne(c => c.FkUser) // Specify the navigation property
                .WithMany(u => u.Clients) // Specify the inverse navigation property
                .HasForeignKey(c => c.FkUserId) // Specify the foreign key
                .OnDelete(DeleteBehavior.Restrict); // Set the delete behavior to Restrict

            // Apply similar configurations for any other relationships that might lead to cascade paths
        }

    }
}
