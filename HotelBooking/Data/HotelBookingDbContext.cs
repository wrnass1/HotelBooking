using Microsoft.EntityFrameworkCore;
using HotelBooking.Models.Entities;

namespace HotelBooking.Data;

public class HotelBookingDbContext : DbContext
{
    public HotelBookingDbContext(DbContextOptions<HotelBookingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Hotel> Hotels { get; set; }
    public DbSet<Room> Rooms { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Hotel configuration
        modelBuilder.Entity<Hotel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Address).IsRequired().HasMaxLength(500);
            entity.Property(e => e.City).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Country).IsRequired().HasMaxLength(100);
            entity.Property(e => e.StarRating).IsRequired();
            entity.HasIndex(e => e.Name);
        });

        // Room configuration
        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RoomNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.RoomType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PricePerNight).HasColumnType("decimal(18,2)");
            entity.HasOne(e => e.Hotel)
                .WithMany(h => h.Rooms)
                .HasForeignKey(e => e.HotelId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.HotelId, e.RoomNumber }).IsUnique();
        });

        // Booking configuration
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GuestName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.GuestEmail).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)");
            entity.HasOne(e => e.Room)
                .WithMany(r => r.Bookings)
                .HasForeignKey(e => e.RoomId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(e => e.GuestEmail);
            entity.HasIndex(e => e.CheckInDate);
            entity.HasIndex(e => e.CheckOutDate);
        });

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(200);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });
    }
}
