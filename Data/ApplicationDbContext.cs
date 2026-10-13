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

            // Configuración de Order y OrderItem (HU-09)
            builder.Entity<Order>(entity =>
            {
                entity.HasKey(o => o.Id);
                entity.Property(o => o.Code).IsRequired().HasMaxLength(20);
                entity.Property(o => o.CustomerName).HasMaxLength(120);
                entity.Property(o => o.Note).HasMaxLength(500);
                entity.Property(o => o.Type).HasConversion<int>();
                entity.Property(o => o.Status).HasConversion<int>();

                entity.HasIndex(o => o.Code).IsUnique();
                // El tablero de cocina filtra por estado y ordena por antigüedad
                entity.HasIndex(o => new { o.Status, o.CreatedAt });
            });

            builder.Entity<OrderItem>(entity =>
            {
                entity.HasKey(i => i.Id);
                entity.Property(i => i.DishName).IsRequired().HasMaxLength(150);
                entity.Property(i => i.UnitPrice).HasPrecision(10, 2);

                entity.HasOne(i => i.Order)
                    .WithMany(o => o.Items)
                    .HasForeignKey(i => i.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(i => i.Dish)
                    .WithMany()
                    .HasForeignKey(i => i.DishId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}