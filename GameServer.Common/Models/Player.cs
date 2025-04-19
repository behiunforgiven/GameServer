using System;

namespace GameServer.Common.Models
{
    public class Player
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string ConnectionId { get; set; }
        public bool IsConnected { get; set; }
        public DateTime LastActivity { get; set; }
        public PlayerStats Stats { get; set; } = new PlayerStats();
    }

    public class PlayerStats
    {
        public int GamesPlayed { get; set; }
        public int GamesWon { get; set; }
        public int GamesLost { get; set; }
        public int GamesTied { get; set; }
        public int Rating { get; set; } = 1000; // Default ELO rating
    }
}