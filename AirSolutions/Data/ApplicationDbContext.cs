using Microsoft.EntityFrameworkCore;
using AirSolutions.Models;

namespace AirSolutions.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteLine> QuoteLines => Set<QuoteLine>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.ClientType).HasMaxLength(50);
            e.Property(c => c.FirstName).HasMaxLength(200);
            e.Property(c => c.LastName).HasMaxLength(200);
            e.Property(c => c.CompanyName).HasMaxLength(300);
            e.Property(c => c.DocumentNumber).HasMaxLength(100);
            e.Property(c => c.Phone).HasMaxLength(50);
            e.Property(c => c.SecondaryPhone).HasMaxLength(50);
            e.Property(c => c.Email).HasMaxLength(200);
            e.Property(c => c.Address).HasMaxLength(500);
            e.Property(c => c.Sector).HasMaxLength(200);
            e.Property(c => c.City).HasMaxLength(200);
            e.Property(c => c.Notes).HasMaxLength(2000);
            e.Property(c => c.PreferredPaymentMethod).HasMaxLength(100);
        });

        modelBuilder.Entity<CatalogItem>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).HasMaxLength(300).IsRequired();
            e.Property(c => c.Description).HasMaxLength(1000);
            e.Property(c => c.ItemType).HasMaxLength(50).IsRequired();
            e.Property(c => c.Nivel).HasMaxLength(50);
            e.Property(c => c.SKU).HasMaxLength(100);
            e.Property(c => c.Unit).HasMaxLength(50);
            e.Property(c => c.BasePrice).HasPrecision(18, 2);
            e.Property(c => c.Cost).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Quote>(e =>
        {
            e.HasKey(q => q.Id);
            e.Property(q => q.Name).HasMaxLength(300);
            e.Property(q => q.Description).HasMaxLength(2000);
            e.HasOne(q => q.Client)
                .WithMany()
                .HasForeignKey(q => q.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<QuoteLine>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.Name).HasMaxLength(300).IsRequired();
            e.Property(l => l.Description).HasMaxLength(2000);
            e.Property(l => l.Quantity).HasPrecision(18, 2);
            e.Property(l => l.UnitPrice).HasPrecision(18, 2);
            e.Property(l => l.DiscountValue).HasPrecision(5, 2);
            e.Property(l => l.DiscountTotal).HasPrecision(18, 2);
            e.Property(l => l.TaxRate).HasPrecision(5, 2);
            e.Property(l => l.TaxTotal).HasPrecision(18, 2);
            e.Property(l => l.LineSubtotal).HasPrecision(18, 2);
            e.Property(l => l.LineTotal).HasPrecision(18, 2);

            e.HasOne(l => l.Quote)
                .WithMany(q => q.Lines)
                .HasForeignKey(l => l.QuoteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Username).HasMaxLength(100).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
            e.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            e.Property(u => u.Email).HasMaxLength(200);
            e.Property(u => u.Role).HasMaxLength(50).IsRequired();
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
            e.HasIndex(u => u.IsActive);
        });
    }
}

