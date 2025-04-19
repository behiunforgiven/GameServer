using Microsoft.EntityFrameworkCore;

namespace GameServer.Core.Data
{
    public class GameDbContext : DbContext
    {
        public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
        {
        }

        public DbSet<UserAccount> Users { get; set; }
        public DbSet<GameHistory> GameHistories { get; set; }
        public DbSet<PlayerStatistics> PlayerStatistics { get; set; }
        public DbSet<ServerLog> ServerLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure UserAccount
            modelBuilder.Entity<UserAccount>()
                .HasIndex(u => u.Username)
                .IsUnique();

            // Configure GameHistory
            modelBuilder.Entity<GameHistory>()
                .HasMany(g => g.PlayerResults)
                .WithOne()
                .HasForeignKey(pr => pr.GameHistoryId);

            // Configure PlayerStatistics
            modelBuilder.Entity<PlayerStatistics>()
                .HasOne(ps => ps.User)
                .WithOne(u => u.Statistics)
                .HasForeignKey<PlayerStatistics>(ps => ps.UserId);

            base.OnModelCreating(modelBuilder);
        }
    }

    public class UserAccount
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string Email { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsBanned { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastLogin { get; set; }
        public PlayerStatistics Statistics { get; set; }
    }

    public class GameHistory
    {
        public int Id { get; set; }
        public string GameType { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string GameState { get; set; }
        public List<PlayerResult> PlayerResults { get; set; } = new List<PlayerResult>();
        public string GameData { get; set; } // JSON serialized game data
    }

    public class PlayerResult
    {
        public int Id { get; set; }
        public int GameHistoryId { get; set; }
        public int UserId { get; set; }
        public string Result { get; set; } // Win, Loss, Tie
        public int ScoreChange { get; set; }
    }

    public class PlayerStatistics
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public UserAccount User { get; set; }
        public int GamesPlayed { get; set; }
        public int GamesWon { get; set; }
        public int GamesLost { get; set; }
        public int GamesTied { get; set; }
        public int Rating { get; set; } = 1000; // Default ELO rating
    }

    public class ServerLog
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string LogLevel { get; set; }
        public string Category { get; set; }
        public string Message { get; set; }
        public string Exception { get; set; }
    }
}