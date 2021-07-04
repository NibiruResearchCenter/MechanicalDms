using MechanicalDms.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace MechanicalDms.Database
{
    public class DmsDbContext : DbContext
    {
        public DbSet<KaiheilaUser> KaiheilaUsers { get; set; }
        public DbSet<BilibiliUser> BilibiliUsers { get; set; }
        public DbSet<DiscordUser> DiscordUser { get; set; }
        public DbSet<MinecraftPlayer> MinecraftPlayers { get; set; }
        
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(DbConfig.ConnectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BilibiliUser>()
                .Property(p => p.Uid)
                .ValueGeneratedNever();

            modelBuilder.Entity<KaiheilaUser>()
                .Property(p => p.Uid)
                .ValueGeneratedNever();

            modelBuilder.Entity<DiscordUser>()
                .Property(p => p.Uid)
                .ValueGeneratedNever();

            modelBuilder.Entity<MinecraftPlayer>()
                .Property(p => p.Uuid)
                .ValueGeneratedNever();
        }
    }
}
