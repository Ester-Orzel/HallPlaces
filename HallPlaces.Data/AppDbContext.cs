using HallPlaces.Core.Entities;
using HallPlaces.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace HallPlaces.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Hall> Halls => Set<Hall>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();

    // Fluent API: everything the entity classes cannot say by themselves.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Hall>(hall =>
        {
            hall.Property(h => h.Name).IsRequired().HasMaxLength(100);
            hall.Property(h => h.Address).IsRequired().HasMaxLength(200);

            // List<int> is stored as ONE column (a PostgreSQL integer array), e.g. {10,12,14}.
            hall.PrimitiveCollection(h => h.RowSeatCounts).IsRequired();
        });

        modelBuilder.Entity<Event>(ev =>
        {
            ev.Property(e => e.Title).IsRequired().HasMaxLength(200);
            ev.Property(e => e.Description).HasMaxLength(2000);
            ev.Property(e => e.ImageUrl).HasMaxLength(500);

            // The events list is filtered and sorted by date.
            ev.HasIndex(e => e.StartsAt);

            // One-to-many: Hall -> Events.
            // Restrict: a hall that has events cannot be deleted.
            ev.HasOne(e => e.Hall)
              .WithMany(h => h.Events)
              .HasForeignKey(e => e.HallId)
              .OnDelete(DeleteBehavior.Restrict);

            // Many-to-many: Event <-> Category, through the join table "EventCategories".
            ev.HasMany(e => e.Categories)
              .WithMany(c => c.Events)
              .UsingEntity(join => join.ToTable("EventCategories"));
        });

        modelBuilder.Entity<Seat>(seat =>
        {
            seat.Property(s => s.Price).HasPrecision(10, 2);

            // One-to-many: Event -> Seats.
            // Cascade: deleting an event (allowed only without bookings) deletes its seats.
            seat.HasOne(s => s.Event)
                .WithMany(e => e.Seats)
                .HasForeignKey(s => s.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            // The same seat cannot exist twice in the same event.
            seat.HasIndex(s => new { s.EventId, s.RowNumber, s.SeatNumber }).IsUnique();

            // The concurrency token (Version) is defined by [ConcurrencyCheck] on the entity.
        });

        modelBuilder.Entity<Booking>(booking =>
        {
            booking.HasOne(b => b.User)
                   .WithMany(u => u.Bookings)
                   .HasForeignKey(b => b.UserId)
                   .OnDelete(DeleteBehavior.Restrict);

            booking.HasOne(b => b.Seat)
                   .WithMany(s => s.Bookings)
                   .HasForeignKey(b => b.SeatId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Safety net in the database itself: at most ONE active booking per seat.
            // Cancelled bookings are not part of the index, so the history is kept.
            booking.HasIndex(b => b.SeatId)
                   .IsUnique()
                   .HasFilter($"\"Status\" = {(int)BookingStatus.Active}");
        });

        modelBuilder.Entity<User>(user =>
        {
            user.Property(u => u.FullName).IsRequired().HasMaxLength(100);
            user.Property(u => u.Email).IsRequired().HasMaxLength(200);
            user.Property(u => u.PasswordHash).IsRequired();

            // Two users cannot register with the same email.
            user.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Category>(category =>
        {
            category.Property(c => c.Name).IsRequired().HasMaxLength(50);
            category.HasIndex(c => c.Name).IsUnique();
        });
    }

    // Written ONCE here, so no service ever has to remember it:
    // every seat that is about to be updated gets a new Version.
    // EF keeps the OLD value for the WHERE clause and writes the NEW one.
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Seat>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.Version = Guid.NewGuid();
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
