using EventRegistration.API.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventRegistration.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Event> Events { get; set; }
    public DbSet<TicketType> TicketTypes { get; set; }
    public DbSet<Registration> Registrations { get; set; }
    public DbSet<DiscountCode> DiscountCodes { get; set; }
    public DbSet<WaitlistEntry> WaitlistEntries { get; set; }
    public DbSet<CancellationPolicy> CancellationPolicies { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Event configuration
        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);

            entity.HasMany(e => e.TicketTypes)
                .WithOne(t => t.Event)
                .HasForeignKey(t => t.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Registrations)
                .WithOne(r => r.Event)
                .HasForeignKey(r => r.EventId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.DiscountCodes)
                .WithOne(d => d.Event)
                .HasForeignKey(d => d.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CancellationPolicy)
                .WithOne(c => c.Event)
                .HasForeignKey<CancellationPolicy>(c => c.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TicketType configuration
        modelBuilder.Entity<TicketType>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(200);
            entity.Property(t => t.Price).HasColumnType("decimal(18,2)");

            entity.HasMany(t => t.Registrations)
                .WithOne(r => r.TicketType)
                .HasForeignKey(r => r.TicketTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Registration configuration
        modelBuilder.Entity<Registration>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(r => r.LastName).IsRequired().HasMaxLength(100);
            entity.Property(r => r.Email).IsRequired().HasMaxLength(255);
            entity.Property(r => r.Status).IsRequired().HasMaxLength(50);
            entity.Property(r => r.TotalAmount).HasColumnType("decimal(18,2)");
        });

        // DiscountCode configuration
        modelBuilder.Entity<DiscountCode>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Code).IsRequired().HasMaxLength(50);
            entity.Property(d => d.DiscountType).IsRequired().HasMaxLength(50);
            entity.Property(d => d.DiscountValue).HasColumnType("decimal(18,2)");
            entity.Property(d => d.Status).IsRequired().HasMaxLength(50);

            entity.HasIndex(d => new { d.EventId, d.Code }).IsUnique();
        });

        // WaitlistEntry configuration
        modelBuilder.Entity<WaitlistEntry>(entity =>
        {
            entity.HasKey(w => w.Id);
            entity.Property(w => w.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(w => w.LastName).IsRequired().HasMaxLength(100);
            entity.Property(w => w.Email).IsRequired().HasMaxLength(255);

            entity.HasOne(w => w.Event)
                .WithMany()
                .HasForeignKey(w => w.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(w => w.TicketType)
                .WithMany()
                .HasForeignKey(w => w.TicketTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // CancellationPolicy configuration
        modelBuilder.Entity<CancellationPolicy>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.CancellationFee).HasColumnType("decimal(18,2)");
        });
    }
}
