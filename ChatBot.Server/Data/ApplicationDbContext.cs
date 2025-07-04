using ChatBot.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatBot.Server.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<ChatHistory> ChatHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ChatHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SessionId).IsRequired();
                entity.Property(e => e.UserMessage).IsRequired();
                entity.Property(e => e.BotResponse).IsRequired();
                entity.Property(e => e.Timestamp).IsRequired();
                
                // Add indexes
                entity.HasIndex(e => e.SessionId);
                entity.HasIndex(e => e.Timestamp);
            });
        }
    }
} 