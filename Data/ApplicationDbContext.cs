using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Models;

namespace software_elviejomadero.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Dish> Dishes => Set<Dish>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Índice único para DNI de ApplicationUser
            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.DNI)
                .IsUnique();

            // Configuración de Category y Dish
            builder.Entity<Category>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
            });

            builder.Entity<Dish>(entity =>
            {
                entity.HasKey(d => d.Id);
                entity.Property(d => d.Name).IsRequired().HasMaxLength(150);
                entity.Property(d => d.Price).HasPrecision(10, 2);

                entity.HasOne(d => d.Category)
                    .WithMany(c => c.Dishes)
                    .HasForeignKey(d => d.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configuración de Order y OrderItem (HU-06)
            builder.Entity<Order>(entity =>
            {
                entity.HasKey(o => o.Id);
                entity.HasIndex(o => o.OrderCode).IsUnique();
                entity.Property(o => o.OrderCode).IsRequired().HasMaxLength(15);
                entity.Property(o => o.OrderType).IsRequired().HasMaxLength(30);
                entity.Property(o => o.Status).IsRequired().HasMaxLength(30);
                entity.Property(o => o.CustomerName).IsRequired().HasMaxLength(120);
                entity.Property(o => o.CustomerPhone).IsRequired().HasMaxLength(20);
                entity.Property(o => o.PaymentMethod).IsRequired().HasMaxLength(30);
                entity.Property(o => o.TotalAmount).HasPrecision(10, 2);

                entity.HasOne(o => o.Receptionist)
                    .WithMany()
                    .HasForeignKey(o => o.ReceptionistId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<OrderItem>(entity =>
            {
                entity.HasKey(oi => oi.Id);
                entity.Property(oi => oi.DishName).IsRequired().HasMaxLength(150);
                entity.Property(oi => oi.UnitPrice).HasPrecision(10, 2);
                entity.Property(oi => oi.Subtotal).HasPrecision(10, 2);

                entity.HasOne(oi => oi.Order)
                    .WithMany(o => o.Items)
                    .HasForeignKey(oi => oi.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(oi => oi.Dish)
                    .WithMany()
                    .HasForeignKey(oi => oi.DishId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
