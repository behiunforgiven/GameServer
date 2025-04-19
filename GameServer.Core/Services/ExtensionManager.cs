using GameServer.Common.Extensions;
using GameServer.Common.Models;
using GameServer.Extensions;
using Microsoft.AspNetCore.SignalR;
using GameServer.Core.Hubs;
using LogLevel = GameServer.Common.Extensions.LogLevel;

namespace GameServer.Core.Services
{
    public class ExtensionManager
    {
        private readonly ILogger<ExtensionManager> _logger;
        private readonly ExtensionLoader _extensionLoader;
        private readonly IHubContext<GameHub> _hubContext;
        private readonly Dictionary<string, IGameExtension> _extensions;
        private readonly Dictionary<string, ExtensionContextImpl> _extensionContexts = new Dictionary<string, ExtensionContextImpl>();

        public ExtensionManager(
            ILogger<ExtensionManager> logger,
            ExtensionLoader extensionLoader,
            IHubContext<GameHub> hubContext,
            GameRoomManager roomManager)
        {
            _logger = logger;
            _extensionLoader = extensionLoader;
            _hubContext = hubContext;
            _extensions = _extensionLoader.LoadExtensions();

            // Create extension contexts
            foreach (var extension in _extensions)
            {
                _extensionContexts[extension.Key] = new ExtensionContextImpl(
                    _logger,
                    _hubContext,
                    roomManager,
                    extension.Key);
            }
        }

        public void Initialize()
        {
            _logger.LogInformation("Initializing ExtensionManager");
            
            // Clear existing extensions
            _extensions.Clear();
            
            // Repopulate with newly loaded extensions
            var loadedExtensions = _extensionLoader.LoadExtensions();
            foreach (var extension in loadedExtensions)
            {
                _extensions.Add(extension.Key, extension.Value);
            }
            
            _logger.LogInformation("Loaded {Count} game extensions", _extensions.Count);
        }

        public IGameExtension GetExtension(string gameType)
        {
            if (_extensions.TryGetValue(gameType, out var extension))
            {
                return extension;
            }
            
            _logger.LogWarning("Extension not found for game type: {GameType}", gameType);
            return null;
        }

        public List<string> GetAvailableGameTypes()
        {
            return new List<string>(_extensions.Keys);
        }

        public bool IsGameTypeSupported(string gameType)
        {
            return _extensions.ContainsKey(gameType);
        }

        public Task<object> InitializeGameDataAsync(string gameType, int playerCount)
        {
            var extension = GetExtension(gameType);
            if (extension == null)
            {
                _logger.LogError("Cannot initialize game data for unsupported game type: {GameType}", gameType);
                return Task.FromResult<object>(null);
            }

            try
            {
                // Changed from CreateInitialGameState to Initialize
                var gameData = extension.Initialize(playerCount);
                return Task.FromResult(gameData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing game data for {GameType}", gameType);
                return Task.FromResult<object>(null);
            }
        }

        public Task<bool> ValidateMoveAsync(string gameType, GameMove move, object gameData)
        {
            var extension = GetExtension(gameType);
            if (extension == null)
            {
                _logger.LogError("Cannot validate move for unsupported game type: {GameType}", gameType);
                return Task.FromResult(false);
            }

            try
            {
                // Changed from ValidateMove to IsValidMove
                var isValid = extension.IsValidMove(move, gameData);
                return Task.FromResult(isValid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating move for {GameType}", gameType);
                return Task.FromResult(false);
            }
        }

        public Task<object> ProcessMoveAsync(string gameType, GameMove move, object gameData)
        {
            var extension = GetExtension(gameType);
            if (extension == null)
            {
                _logger.LogError("Cannot process move for unsupported game type: {GameType}", gameType);
                return Task.FromResult<object>(null);
            }

            try
            {
                // Changed from ApplyMove to ExecuteMove
                var updatedGameData = extension.ExecuteMove(move, gameData);
                return Task.FromResult(updatedGameData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing move for {GameType}", gameType);
                return Task.FromResult<object>(null);
            }
        }

        public Task<bool> IsGameOverAsync(string gameType, object gameData)
        {
            var extension = GetExtension(gameType);
            if (extension == null)
            {
                _logger.LogError("Cannot check game over for unsupported game type: {GameType}", gameType);
                return Task.FromResult(false);
            }

            try
            {
                // Changed from CheckGameOver to IsGameComplete
                var isGameOver = extension.IsGameComplete(gameData);
                return Task.FromResult(isGameOver);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking game over for {GameType}", gameType);
                return Task.FromResult(false);
            }
        }

        public Task<Dictionary<string, GameResult>> GetGameResultsAsync(string gameType, object gameData, Dictionary<string, Player> players)
        {
            var extension = GetExtension(gameType);
            if (extension == null)
            {
                _logger.LogError("Cannot get game results for unsupported game type: {GameType}", gameType);
                return Task.FromResult(new Dictionary<string, GameResult>());
            }

            try
            {
                // Changed from CalculateResults to DetermineResults
                var results = extension.DetermineResults(gameData, players);
                return Task.FromResult(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting game results for {GameType}", gameType);
                return Task.FromResult(new Dictionary<string, GameResult>());
            }
        }

        public IExtensionContext GetExtensionContext(string gameType)
        {
            if (_extensionContexts.TryGetValue(gameType, out var context))
            {
                return context;
            }
            return null;
        }

        private class ExtensionContextImpl : IExtensionContext
        {
            private readonly ILogger _logger;
            private readonly IHubContext<GameHub> _hubContext;
            private readonly GameRoomManager _roomManager;
            private readonly string _gameType;

            public ExtensionContextImpl(
                ILogger logger,
                IHubContext<GameHub> hubContext,
                GameRoomManager roomManager,
                string gameType)
            {
                _logger = logger;
                _hubContext = hubContext;
                _roomManager = roomManager;
                _gameType = gameType;
            }

            public async Task BroadcastToRoomAsync(string roomId, string method, object data)
            {
                await _hubContext.Clients.Group($"room_{roomId}").SendAsync(method, data);
            }

            public async Task SendToPlayerAsync(string playerId, string method, object data)
            {
                var room = _roomManager.GetAllRooms().Find(r => r.Players.ContainsKey(playerId));
                if (room != null && room.Players.TryGetValue(playerId, out var player))
                {
                    await _hubContext.Clients.Client(player.ConnectionId).SendAsync(method, data);
                }
            }

            public async Task<Player> GetPlayerAsync(string playerId)
            {
                var room = _roomManager.GetAllRooms().Find(r => r.Players.ContainsKey(playerId));
                if (room != null && room.Players.TryGetValue(playerId, out var player))
                {
                    return player;
                }
                return null;
            }

            public async Task UpdateGameRoomAsync(GameRoom room)
            {
                var existingRoom = _roomManager.GetRoom(room.Id);
                if (existingRoom != null)
                {
                    // Update the room (in a real implementation, you'd need to handle this properly)
                    // For now, we'll just broadcast the updated state
                    await BroadcastToRoomAsync(room.Id, "RoomUpdated", room);
                }
            }

            public async Task EndGameAsync(string roomId, Dictionary<string, GameResult> results)
            {
                await _roomManager.EndGameAsync(roomId, GameState.Finished, results);
                await BroadcastToRoomAsync(roomId, "GameEnded", results);
            }

            

            public async Task LogAsync(string message, LogLevel level = LogLevel.Info)
            {
                switch (level)
                {
                    case LogLevel.Debug:
                        _logger.LogDebug("[{GameType}] {Message}", _gameType, message);
                        break;
                    case LogLevel.Info:
                        _logger.LogInformation("[{GameType}] {Message}", _gameType, message);
                        break;
                    case LogLevel.Warning:
                        _logger.LogWarning("[{GameType}] {Message}", _gameType, message);
                        break;
                    case LogLevel.Error:
                        _logger.LogError("[{GameType}] {Message}", _gameType, message);
                        break;
                }
            }
        }
    }
}