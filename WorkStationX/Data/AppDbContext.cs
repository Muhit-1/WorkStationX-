using Microsoft.EntityFrameworkCore;
using WorkStationX.Infrastructure;
using WorkStationX.Models;

namespace WorkStationX.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskSession> TaskSessions => Set<TaskSession>();
    public DbSet<TimeBankEntry> TimeBankEntries => Set<TimeBankEntry>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceItem> WorkspaceItems => Set<WorkspaceItem>();
    public DbSet<WorkspaceItemLink> WorkspaceItemLinks => Set<WorkspaceItemLink>();
    public DbSet<ChromeProfile> ChromeProfiles => Set<ChromeProfile>();
    public DbSet<Reminder> Reminders => Set<Reminder>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<TaskItem>(e =>
        {
            e.Property(x => x.Title).IsRequired().HasMaxLength(400);
            e.HasMany(x => x.Sessions)
                .WithOne(s => s.TaskItem!)
                .HasForeignKey(s => s.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.TimeBankEntries)
                .WithOne(t => t.TaskItem!)
                .HasForeignKey(t => t.TaskItemId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Workspace>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<WorkspaceItem>(e =>
        {
            e.Property(x => x.DisplayName).IsRequired().HasMaxLength(300);
            e.Property(x => x.Target).IsRequired().HasMaxLength(2000);
            e.HasOne(x => x.ChromeProfile)
                .WithMany()
                .HasForeignKey(x => x.ChromeProfileId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Many-to-many join, so one item can belong to several workspaces.
        b.Entity<WorkspaceItemLink>(e =>
        {
            e.HasOne(x => x.Workspace!)
                .WithMany(w => w.ItemLinks)
                .HasForeignKey(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.WorkspaceItem!)
                .WithMany(i => i.WorkspaceLinks)
                .HasForeignKey(x => x.WorkspaceItemId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.WorkspaceId, x.WorkspaceItemId }).IsUnique();
        });

        b.Entity<ChromeProfile>(e =>
        {
            e.Property(x => x.FriendlyName).IsRequired().HasMaxLength(300);
            e.Property(x => x.ProfileDirectory).IsRequired().HasMaxLength(200);
            e.HasIndex(x => x.ProfileDirectory).IsUnique();
        });

        b.Entity<Reminder>(e =>
        {
            e.Property(x => x.Message).IsRequired().HasMaxLength(1000);
        });
    }
}

/// <summary>
/// Used only by `dotnet ef migrations add` at design time. Without this the EF
/// tools cannot construct the context because the app builds it through DI.
/// </summary>
public class AppDbContextFactory
    : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        AppPaths.EnsureCreated();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={AppPaths.DatabaseFile}")
            .Options;
        return new AppDbContext(options);
    }
}
