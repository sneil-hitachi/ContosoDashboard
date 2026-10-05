using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Models;

namespace ContosoDashboard.Data;

public class ApplicationDbContext : DbContext
{
    private static readonly DateTime SeedDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<TaskItem> Tasks { get; set; } = null!;
    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<TaskComment> TaskComments { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<ProjectMember> ProjectMembers { get; set; } = null!;
    public DbSet<Announcement> Announcements { get; set; } = null!;
    public DbSet<Document> Documents { get; set; } = null!;
    public DbSet<DocumentTag> DocumentTags { get; set; } = null!;
    public DbSet<DocumentShare> DocumentShares { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User relationships
        modelBuilder.Entity<User>()
            .HasMany(u => u.AssignedTasks)
            .WithOne(t => t.AssignedUser)
            .HasForeignKey(t => t.AssignedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasMany(u => u.CreatedTasks)
            .WithOne(t => t.CreatedByUser)
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasMany(u => u.ManagedProjects)
            .WithOne(p => p.ProjectManager)
            .HasForeignKey(p => p.ProjectManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure indexes for performance
        modelBuilder.Entity<TaskItem>()
            .HasIndex(t => t.AssignedUserId);

        modelBuilder.Entity<TaskItem>()
            .HasIndex(t => t.Status);

        modelBuilder.Entity<TaskItem>()
            .HasIndex(t => t.DueDate);

        modelBuilder.Entity<Project>()
            .HasIndex(p => p.ProjectManagerId);

        modelBuilder.Entity<Project>()
            .HasIndex(p => p.Status);

        modelBuilder.Entity<Notification>()
            .HasIndex(n => new { n.UserId, n.IsRead });

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Document>()
            .HasOne(document => document.Uploader)
            .WithMany()
            .HasForeignKey(document => document.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Document>()
            .HasOne(document => document.Project)
            .WithMany()
            .HasForeignKey(document => document.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Document>()
            .HasIndex(document => document.FilePath)
            .IsUnique();
        modelBuilder.Entity<Document>().HasIndex(document => document.UploadedByUserId);
        modelBuilder.Entity<Document>().HasIndex(document => document.ProjectId);
        modelBuilder.Entity<Document>().HasIndex(document => document.Category);
        modelBuilder.Entity<Document>().HasIndex(document => document.UploadedAtUtc);

        modelBuilder.Entity<DocumentTag>()
            .HasOne(tag => tag.Document)
            .WithMany(document => document.Tags)
            .HasForeignKey(tag => tag.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<DocumentTag>()
            .HasIndex(tag => new { tag.DocumentId, tag.NormalizedValue })
            .IsUnique();
        modelBuilder.Entity<DocumentTag>()
            .HasIndex(tag => tag.NormalizedValue);

        modelBuilder.Entity<DocumentShare>()
            .ToTable(table => table.HasCheckConstraint(
                "CK_DocumentShares_OneRecipient",
                "([RecipientUserId] IS NOT NULL AND [RecipientDepartment] IS NULL) OR ([RecipientUserId] IS NULL AND [RecipientDepartment] IS NOT NULL)"));
        modelBuilder.Entity<DocumentShare>()
            .HasOne(share => share.Document)
            .WithMany(document => document.Shares)
            .HasForeignKey(share => share.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<DocumentShare>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(share => share.GrantedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DocumentShare>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(share => share.RecipientUserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DocumentShare>()
            .HasIndex(share => new { share.DocumentId, share.RecipientUserId })
            .IsUnique()
            .HasFilter("[RecipientUserId] IS NOT NULL");
        modelBuilder.Entity<DocumentShare>()
            .HasIndex(share => new { share.DocumentId, share.RecipientDepartment })
            .IsUnique()
            .HasFilter("[RecipientDepartment] IS NOT NULL");

        // Seed initial data
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // Seed an admin user
        modelBuilder.Entity<User>().HasData(
            new User
            {
                UserId = 1,
                Email = "admin@contoso.com",
                DisplayName = "System Administrator",
                Department = "IT",
                JobTitle = "Administrator",
                Role = UserRole.Administrator,
                AvailabilityStatus = AvailabilityStatus.Available,
                CreatedDate = SeedDate,
                EmailNotificationsEnabled = true,
                InAppNotificationsEnabled = true
            },
            new User
            {
                UserId = 2,
                Email = "camille.nicole@contoso.com",
                DisplayName = "Camille Nicole",
                Department = "Engineering",
                JobTitle = "Project Manager",
                Role = UserRole.ProjectManager,
                AvailabilityStatus = AvailabilityStatus.Available,
                CreatedDate = SeedDate,
                EmailNotificationsEnabled = true,
                InAppNotificationsEnabled = true
            },
            new User
            {
                UserId = 3,
                Email = "floris.kregel@contoso.com",
                DisplayName = "Floris Kregel",
                Department = "Engineering",
                JobTitle = "Team Lead",
                Role = UserRole.TeamLead,
                AvailabilityStatus = AvailabilityStatus.Available,
                CreatedDate = SeedDate,
                EmailNotificationsEnabled = true,
                InAppNotificationsEnabled = true
            },
            new User
            {
                UserId = 4,
                Email = "ni.kang@contoso.com",
                DisplayName = "Ni Kang",
                Department = "Engineering",
                JobTitle = "Software Engineer",
                Role = UserRole.Employee,
                AvailabilityStatus = AvailabilityStatus.Available,
                CreatedDate = SeedDate,
                EmailNotificationsEnabled = true,
                InAppNotificationsEnabled = true
            }
        );

        // Seed a sample project
        modelBuilder.Entity<Project>().HasData(
            new Project
            {
                ProjectId = 1,
                Name = "ContosoDashboard Development",
                Description = "Internal employee productivity dashboard",
                ProjectManagerId = 2,
                StartDate = SeedDate.AddDays(-30),
                TargetCompletionDate = SeedDate.AddDays(60),
                Status = ProjectStatus.Active,
                CreatedDate = SeedDate.AddDays(-30),
                UpdatedDate = SeedDate
            }
        );

        // Seed sample tasks
        modelBuilder.Entity<TaskItem>().HasData(
            new TaskItem
            {
                TaskId = 1,
                Title = "Design database schema",
                Description = "Create entity relationship diagram and database design",
                Priority = TaskPriority.High,
                Status = Models.TaskStatus.Completed,
                DueDate = SeedDate.AddDays(-20),
                AssignedUserId = 4,
                CreatedByUserId = 2,
                ProjectId = 1,
                CreatedDate = SeedDate.AddDays(-30),
                UpdatedDate = SeedDate.AddDays(-20)
            },
            new TaskItem
            {
                TaskId = 2,
                Title = "Implement authentication",
                Description = "Set up Microsoft Entra ID authentication",
                Priority = TaskPriority.Critical,
                Status = Models.TaskStatus.InProgress,
                DueDate = SeedDate.AddDays(5),
                AssignedUserId = 4,
                CreatedByUserId = 2,
                ProjectId = 1,
                CreatedDate = SeedDate.AddDays(-25),
                UpdatedDate = SeedDate
            },
            new TaskItem
            {
                TaskId = 3,
                Title = "Create UI mockups",
                Description = "Design user interface mockups for all main pages",
                Priority = TaskPriority.Medium,
                Status = Models.TaskStatus.NotStarted,
                DueDate = SeedDate.AddDays(10),
                AssignedUserId = 4,
                CreatedByUserId = 2,
                ProjectId = 1,
                CreatedDate = SeedDate.AddDays(-20),
                UpdatedDate = SeedDate.AddDays(-20)
            }
        );

        // Seed project members
        modelBuilder.Entity<ProjectMember>().HasData(
            new ProjectMember
            {
                ProjectMemberId = 1,
                ProjectId = 1,
                UserId = 3,
                Role = "TeamLead",
                AssignedDate = SeedDate.AddDays(-30)
            },
            new ProjectMember
            {
                ProjectMemberId = 2,
                ProjectId = 1,
                UserId = 4,
                Role = "Developer",
                AssignedDate = SeedDate.AddDays(-30)
            }
        );

        // Seed announcement
        modelBuilder.Entity<Announcement>().HasData(
            new Announcement
            {
                AnnouncementId = 1,
                Title = "Welcome to ContosoDashboard",
                Content = "Welcome to the new ContosoDashboard application. This platform will help you manage your tasks and projects more efficiently.",
                CreatedByUserId = 1,
                PublishDate = SeedDate,
                ExpiryDate = SeedDate.AddDays(30),
                IsActive = true
            }
        );
    }
}
