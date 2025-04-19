using System.Collections.Concurrent;
using GameServer.Common.Models;

namespace GameServer.Core.Services
{
    public class PlayerManager
    {
        private readonly ILogger<PlayerManager> _logger;
        private readonly ConcurrentDictionary<string, Player> _players = new ConcurrentDictionary<string, Player>();

        public PlayerManager(ILogger<PlayerManager> logger)
        {
            _logger = logger;
        }

        public async Task<Player> CreatePlayerAsync(string username)
        {
            var player = new Player
            {
                Id = Guid.NewGuid().ToString(),
                Username = username,
                IsConnected = false,
                LastActivity = DateTime.UtcNow,
                Stats = new PlayerStats()
            };

            if (_players.TryAdd(player.Id, player))
            {
                _logger.LogInformation("Created player: {PlayerId} ({Username})", player.Id, username);
                return player;
            }

            _logger.LogError("Failed to create player: {Username}", username);
            return null;
        }

        public async Task<Player> GetPlayerAsync(string playerId)
        {
            _players.TryGetValue(playerId, out var player);
            return player;
        }

        // Add this method to the PlayerManager class
        public async Task<Player> GetPlayerByUsernameAsync(string username)
        {
            return _players.Values.FirstOrDefault(p => p.Username == username);
        }

        public async Task<bool> UpdatePlayerAsync(Player player)
        {
            if (_players.TryGetValue(player.Id, out _))
            {
                _players[player.Id] = player;
                return true;
            }
            return false;
        }

        public async Task<bool> DeletePlayerAsync(string playerId)
        {
            return _players.TryRemove(playerId, out _);
        }

        public List<Player> GetAllPlayers()
        {
            return _players.Values.ToList();
        }

        public List<Player> GetOnlinePlayers()
        {
            return _players.Values.Where(p => p.IsConnected).ToList();
        }

        public async Task<bool> SetPlayerConnectionStatusAsync(string playerId, bool isConnected)
        {
            if (_players.TryGetValue(playerId, out var player))
            {
                player.IsConnected = isConnected;
                player.LastActivity = DateTime.UtcNow;
                _players[playerId] = player;
                
                _logger.LogInformation("Player {PlayerId} connection status set to {IsConnected}", 
                    playerId, isConnected);
                return true;
            }
            
            _logger.LogWarning("Failed to set connection status for non-existent player: {PlayerId}", playerId);
            return false;
        }

        public async Task<bool> UpdatePlayerStatsAsync(string playerId, GameResult result)
        {
            if (_players.TryGetValue(playerId, out var player))
            {
                player.Stats.GamesPlayed++;
                
                switch (result.Outcome)
                {
                    case GameOutcome.Win:
                        player.Stats.GamesWon++;  // Changed from Wins to GamesWon
                        player.Stats.Rating += result.RatingChange;
                        break;
                    case GameOutcome.Loss:
                        player.Stats.GamesLost++;  // Changed from Losses to GamesLost
                        player.Stats.Rating += result.RatingChange; // Rating change is negative for losses
                        break;
                    case GameOutcome.Draw:
                        player.Stats.GamesTied++;  // Changed from Draws to GamesTied
                        player.Stats.Rating += result.RatingChange;
                        break;
                }
                
                player.LastActivity = DateTime.UtcNow;
                _players[playerId] = player;
                
                _logger.LogInformation("Updated stats for player {PlayerId}: {Outcome}, Rating change: {RatingChange}", 
                    playerId, result.Outcome, result.RatingChange);
                return true;
            }
            
            _logger.LogWarning("Failed to update stats for non-existent player: {PlayerId}", playerId);
            return false;
        }

        public async Task<bool> UpdatePlayerActivityAsync(string playerId)
        {
            if (_players.TryGetValue(playerId, out var player))
            {
                player.LastActivity = DateTime.UtcNow;
                _players[playerId] = player;
                return true;
            }
            
            return false;
        }

        public Player GetPlayer(string playerId)
        {
            _players.TryGetValue(playerId, out var player);
            return player;
        }

        public async Task CleanupInactivePlayers(TimeSpan inactivityThreshold)
        {
            var now = DateTime.UtcNow;
            var inactivePlayers = _players.Values
                .Where(p => !p.IsConnected && (now - p.LastActivity) > inactivityThreshold)
                .ToList();

            foreach (var player in inactivePlayers)
            {
                if (_players.TryRemove(player.Id, out _))
                {
                    _logger.LogInformation("Removed inactive player: {PlayerId} ({Username})", 
                        player.Id, player.Username);
                }
            }

            _logger.LogInformation("Cleaned up {Count} inactive players", inactivePlayers.Count);
        }
    }
}