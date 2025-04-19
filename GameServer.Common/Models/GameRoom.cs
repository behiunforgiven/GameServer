using System;
using System.Collections.Generic;

namespace GameServer.Common.Models
{
    public class GameRoom
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string GameType { get; set; }
        public Dictionary<string, Player> Players { get; set; } = new Dictionary<string, Player>();
        // Add spectators collection
        public Dictionary<string, Player> Spectators { get; set; } = new Dictionary<string, Player>();
        public GameState State { get; set; } = GameState.Waiting;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public int MaxPlayers { get; set; } = 2;
        public bool IsPrivate { get; set; }
        public string Password { get; set; }
        public string CurrentTurnPlayerId { get; set; }
        public DateTime TurnStartTime { get; set; }
        public int TurnTimeoutSeconds { get; set; } = 60;
        public object GameData { get; set; } // Custom game state managed by extensions
    }

    public enum GameState
    {
        Waiting,
        Playing,
        Finished,
        Abandoned
    }
}