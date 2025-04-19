using System.Collections.Generic;
using GameServer.Common.Models;

namespace GameServer.Common.Extensions
{
    public interface IGameExtension
    {
        string GameType { get; }
        string DisplayName { get; }
        string Description { get; }
        int MinPlayers { get; }
        int MaxPlayers { get; }
        
        object Initialize(int playerCount);
        bool IsValidMove(GameMove move, object gameState);
        object ExecuteMove(GameMove move, object gameState);
        bool IsGameComplete(object gameState);
        Dictionary<string, GameResult> DetermineResults(object gameState, Dictionary<string, Player> players);
    }
}