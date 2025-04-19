using System.Threading.Tasks;
using GameServer.Common.Models;

namespace GameServer.Common.Extensions
{
    public interface IExtensionContext
    {
        Task BroadcastToRoomAsync(string roomId, string method, object data);
        
        Task SendToPlayerAsync(string playerId, string method, object data);
        
        Task<Player> GetPlayerAsync(string playerId);
        
        Task UpdateGameRoomAsync(GameRoom room);
        
        Task EndGameAsync(string roomId, Dictionary<string, GameResult> results);
        
        Task LogAsync(string message, LogLevel level = LogLevel.Info);
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}