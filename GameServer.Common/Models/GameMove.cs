using System;

namespace GameServer.Common.Models
{
    public class GameMove
    {
        public string PlayerId { get; set; }
        public string RoomId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public object MoveData { get; set; } // Custom move data defined by game extensions
    }
}