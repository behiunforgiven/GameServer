using System;

namespace GameServer.Common.Models
{
    public enum GameOutcome
    {
        Win,
        Loss,
        Draw,
        Abandoned
    }

    public class GameResult
    {
        public string PlayerId { get; set; }
        public GameOutcome Outcome { get; set; }
        public int RatingChange { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string GameType { get; set; }
        public string GameId { get; set; }
        
        public GameResult()
        {
        }
        
        public GameResult(string playerId, GameOutcome outcome, int ratingChange)
        {
            PlayerId = playerId;
            Outcome = outcome;
            RatingChange = ratingChange;
        }
    }
}