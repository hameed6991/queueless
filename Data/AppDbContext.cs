using Microsoft.EntityFrameworkCore;
using Queueless.Models;
using Queueless.Models.Business;


namespace Queueless.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<AppUser> AppUsers { get; set; } = null!;
        public DbSet<BusinessRegistration> BusinessRegistrations { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AppUser>(entity =>
            {
                entity.ToTable("AppUser");        // 👈 your SQL table name
                entity.HasKey(e => e.UserId);     // 👈 define PK explicitly
            });
        }
    }
}
