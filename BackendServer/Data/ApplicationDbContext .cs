using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BackendServer.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public DbSet<ApplicationUser> AppUsers { get; set; } // Avoid conflict with Identity's `Users` DbSet
        public DbSet<Post> Posts { get; set; }


        private static readonly TimeZoneInfo IsraelTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Israel Standard Time");

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Define the relationship between ApplicationUser and Post
            builder.Entity<Post>()
                .HasOne(p => p.User) // Each Post has one User (ApplicationUser)
                .WithMany(u => u.Posts) // Each ApplicationUser can have many Posts
                .HasForeignKey(p => p.UserId) // Foreign key in the Post table that references the Id in AspNetUsers
                .OnDelete(DeleteBehavior.Cascade); // Cascade delete behavior: when a User is deleted, their Posts are deleted

            // Optional: If you want to customize the name of the AspNetUsers table or add any additional Identity-related customizations:
            builder.Entity<ApplicationUser>()
                .ToTable("AspNetUsers"); // This is usually not needed unless you want to rename it

            // You can further customize other aspects of the schema here
            // For example, renaming Post table or customizing properties if needed
            builder.Entity<Post>().ToTable("Posts"); // Optional: You can explicitly specify the table name for Posts if desired
        }

        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries();

            foreach (var entry in entries)
            {
                if (entry.Entity is Post post)
                {
                    var now = TimeZoneInfo.ConvertTime(DateTime.UtcNow, IsraelTimeZone);

                    if (entry.State == EntityState.Added)
                    {
                        post.CreatedAt = now;
                        post.UpdatedAt = now;
                    }

                    if (entry.State == EntityState.Modified)
                    {
                        post.UpdatedAt = now;
                    }
                }
            }
        }
    }
}
