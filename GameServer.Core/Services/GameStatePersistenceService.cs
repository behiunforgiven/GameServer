using System.Text.Json;
using GameServer.Common.Models;
using Microsoft.Extensions.Options;

namespace GameServer.Core.Services
{
    public class GameStatePersistenceService
    {
        private readonly ILogger<GameStatePersistenceService> _logger;
        private readonly GameStatePersistenceOptions _options;
        private readonly string _persistenceDirectory;

        public GameStatePersistenceService(
            ILogger<GameStatePersistenceService> logger,
            IOptions<GameStatePersistenceOptions> options)
        {
            _logger = logger;
            _options = options.Value;
            
            _persistenceDirectory = _options.PersistenceDirectory;
            if (!Directory.Exists(_persistenceDirectory))
            {
                Directory.CreateDirectory(_persistenceDirectory);
            }
        }

        public async Task SaveGameStateAsync(GameRoom room)
        {
            try
            {
                var filePath = GetGameStateFilePath(room.Id);
                var gameState = new PersistentGameState
                {
                    RoomId = room.Id,
                    Name = room.Name,
                    GameType = room.GameType,
                    MaxPlayers = room.MaxPlayers,
                    IsPrivate = room.IsPrivate,
                    Password = room.Password,
                    CreatedAt = room.CreatedAt,
                    State = room.State,
                    Players = room.Players,
                    GameData = room.GameData,
                    CurrentTurnPlayerId = room.CurrentTurnPlayerId,
                    LastUpdated = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(gameState, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(filePath, json);
                _logger.LogDebug("Game state saved for room {RoomId}", room.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save game state for room {RoomId}", room.Id);
            }
        }

        public async Task<PersistentGameState> LoadGameStateAsync(string roomId)
        {
            try
            {
                var filePath = GetGameStateFilePath(roomId);
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("No saved game state found for room {RoomId}", roomId);
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var gameState = JsonSerializer.Deserialize<PersistentGameState>(json);
                
                _logger.LogInformation("Loaded game state for room {RoomId}", roomId);
                return gameState;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load game state for room {RoomId}", roomId);
                return null;
            }
        }

        public async Task<List<PersistentGameState>> LoadAllGameStatesAsync()
        {
            try
            {
                var result = new List<PersistentGameState>();
                var files = Directory.GetFiles(_persistenceDirectory, "*.json");
                
                foreach (var file in files)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(file);
                        var gameState = JsonSerializer.Deserialize<PersistentGameState>(json);
                        result.Add(gameState);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load game state from file {FilePath}", file);
                    }
                }
                
                _logger.LogInformation("Loaded {Count} game states", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load all game states");
                return new List<PersistentGameState>();
            }
        }

        public async Task DeleteGameStateAsync(string roomId)
        {
            try
            {
                var filePath = GetGameStateFilePath(roomId);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogDebug("Deleted game state for room {RoomId}", roomId);
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete game state for room {RoomId}", roomId);
            }
        }

        private string GetGameStateFilePath(string roomId)
        {
            return Path.Combine(_persistenceDirectory, $"{roomId}.json");
        }
    }

    public class GameStatePersistenceOptions
    {
        public string PersistenceDirectory { get; set; } = "GameStates";
    }

    public class PersistentGameState
    {
        public string RoomId { get; set; }
        public string Name { get; set; }
        public string GameType { get; set; }
        public int MaxPlayers { get; set; }
        public bool IsPrivate { get; set; }
        public string Password { get; set; }
        public DateTime CreatedAt { get; set; }
        public GameState State { get; set; }  
        public Dictionary<string, Player> Players { get; set; }
        public object GameData { get; set; }
        public string CurrentTurnPlayerId { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}