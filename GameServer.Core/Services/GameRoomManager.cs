using System.Collections.Concurrent;
using GameServer.Common.Models;

namespace GameServer.Core.Services
{
    public class GameRoomManager
    {
        private readonly ILogger<GameRoomManager> _logger;
        private readonly ExtensionManager _extensionManager;
        private readonly ConcurrentDictionary<string, GameRoom> _rooms = new ConcurrentDictionary<string, GameRoom>();

        private readonly GameStatePersistenceService _persistenceService;

        // Add this field at the top of the class with the other private fields
        private readonly PlayerManager _playerManager;

        // The constructor already has the correct parameter, we just need to ensure
        // the field is properly initialized
        public GameRoomManager(
            ILogger<GameRoomManager> logger,
            PlayerManager playerManager,
            ExtensionManager extensionManager,
            GameStatePersistenceService persistenceService)
        {
            _logger = logger;
            _playerManager = playerManager;
            _extensionManager = extensionManager;
            _persistenceService = persistenceService;
            _rooms = new ConcurrentDictionary<string, GameRoom>();
        }

        public async Task<GameRoom> CreateRoomAsync(string name, string gameType, int maxPlayers, bool isPrivate, string password = null)
        {
            var room = new GameRoom
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                GameType = gameType,
                MaxPlayers = maxPlayers,
                IsPrivate = isPrivate,
                Password = password,
                State = GameState.Waiting,
                CreatedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow
            };

            if (_rooms.TryAdd(room.Id, room))
            {
                _logger.LogInformation("Created game room: {RoomId} ({GameType})", room.Id, gameType);
                return room;
            }

            _logger.LogError("Failed to create game room: {RoomName}", name);
            return null;
        }

        public async Task<bool> JoinRoomAsync(string roomId, Player player, string password = null)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                _logger.LogWarning("Player {PlayerId} tried to join non-existent room {RoomId}", player.Id, roomId);
                return false;
            }

            if (room.State != GameState.Waiting)
            {
                _logger.LogWarning("Player {PlayerId} tried to join room {RoomId} that is not in waiting state", player.Id, roomId);
                return false;
            }

            if (room.Players.Count >= room.MaxPlayers)
            {
                _logger.LogWarning("Player {PlayerId} tried to join full room {RoomId}", player.Id, roomId);
                return false;
            }

            if (room.IsPrivate && password != room.Password)
            {
                _logger.LogWarning("Player {PlayerId} provided incorrect password for room {RoomId}", player.Id, roomId);
                return false;
            }

            room.Players[player.Id] = player;
            room.LastActivity = DateTime.UtcNow;

            _logger.LogInformation("Player {PlayerId} joined room {RoomId}", player.Id, roomId);

            if (room.Players.Count == room.MaxPlayers)
            {
                await StartGameAsync(roomId);
            }

            // Save game state
            await _persistenceService.SaveGameStateAsync(room);

            return true;
        }

        public async Task<bool> LeaveRoomAsync(string roomId, string playerId)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return false;
            }

            if (room.Players.Remove(playerId))
            {
                room.LastActivity = DateTime.UtcNow;
                _logger.LogInformation("Player {PlayerId} left room {RoomId}", playerId, roomId);

                if (room.State == GameState.Playing && room.Players.Count < 2)
                {
                    await EndGameAsync(roomId, GameState.Abandoned);
                }
                else if (room.Players.Count == 0)
                {
                    _rooms.TryRemove(roomId, out _);
                    _logger.LogInformation("Removed empty room {RoomId}", roomId);
                }

                return true;
            }

            return false;
        }

        public async Task<bool> StartGameAsync(string roomId)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return false;
            }

            if (room.State != GameState.Waiting || room.Players.Count < 2)
            {
                return false;
            }

            var extension = _extensionManager.GetExtension(room.GameType);
            if (extension == null)
            {
                _logger.LogError("No extension found for game type: {GameType}", room.GameType);
                return false;
            }

            try
            {
                // Changed from InitializeGameAsync to Initialize
                var gameData = extension.Initialize(room.Players.Count);
                room.GameData = gameData;
                room.State = GameState.Playing;
                
                // Determine the first player (this logic may need to be adjusted)
                room.CurrentTurnPlayerId = room.Players.Keys.First();
                room.TurnStartTime = DateTime.UtcNow;
                room.LastActivity = DateTime.UtcNow;

                _logger.LogInformation("Started game in room {RoomId}, first turn: {PlayerId}", 
                    roomId, room.CurrentTurnPlayerId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize game in room {RoomId}", roomId);
                return false;
            }
        }

        public async Task<bool> ProcessMoveAsync(GameMove move)
        {
            if (!_rooms.TryGetValue(move.RoomId, out var room))
            {
                _logger.LogWarning("Move received for non-existent room: {RoomId}", move.RoomId);
                return false;
            }

            if (room.State != GameState.Playing)
            {
                _logger.LogWarning("Move received for room not in playing state: {RoomId}", move.RoomId);
                return false;
            }

            if (room.CurrentTurnPlayerId != move.PlayerId)
            {
                _logger.LogWarning("Move received from player {PlayerId} out of turn in room {RoomId}", 
                    move.PlayerId, move.RoomId);
                return false;
            }

            var extension = _extensionManager.GetExtension(room.GameType);
            if (extension == null)
            {
                _logger.LogError("No extension found for game type: {GameType}", room.GameType);
                return false;
            }

            try
            {
                // Changed from ValidateMoveAsync to IsValidMove
                if (!extension.IsValidMove(move, room.GameData))
                {
                    _logger.LogWarning("Invalid move from player {PlayerId} in room {RoomId}", 
                        move.PlayerId, move.RoomId);
                    return false;
                }

                // Process the move - changed from ProcessMoveAsync to ExecuteMove
                room.GameData = extension.ExecuteMove(move, room.GameData);
                room.LastActivity = DateTime.UtcNow;

                // Check if the game is over - changed from IsGameOverAsync to IsGameComplete
                if (extension.IsGameComplete(room.GameData))
                {
                    // Changed from GetGameResultsAsync to DetermineResults
                    var results = extension.DetermineResults(room.GameData, room.Players);
                    await EndGameAsync(room.Id, GameState.Finished, results);
                    return true;
                }

                // Determine the next player's turn (simple alternating logic)
                var currentPlayerIndex = new List<string>(room.Players.Keys).IndexOf(room.CurrentTurnPlayerId);
                var nextPlayerIndex = (currentPlayerIndex + 1) % room.Players.Count;
                room.CurrentTurnPlayerId = new List<string>(room.Players.Keys)[nextPlayerIndex];
                room.TurnStartTime = DateTime.UtcNow;

                _logger.LogInformation("Processed move in room {RoomId}, next turn: {PlayerId}", 
                    room.Id, room.CurrentTurnPlayerId);
                    
                // Save game state
                await _persistenceService.SaveGameStateAsync(room);
                    
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing move: {Message}", ex.Message);
                return false;
            }
        }

        public async Task EndGameAsync(string roomId, GameState endState, Dictionary<string, GameResult> results = null)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return;
            }

            room.State = endState;
            room.LastActivity = DateTime.UtcNow;

            _logger.LogInformation("Game ended in room {RoomId} with state {State}", roomId, endState);

            // Update player statistics if the game finished normally
            if (endState == GameState.Finished && results != null)
            {
                foreach (var player in room.Players.Values)
                {
                    if (results.TryGetValue(player.Id, out var result))
                    {
                        switch (result.Outcome)
                        {
                            case GameOutcome.Win:
                                player.Stats.GamesWon++;
                                break;
                            case GameOutcome.Loss:
                                player.Stats.GamesLost++;
                                break;
                            case GameOutcome.Draw:
                                player.Stats.GamesTied++;
                                break;
                        }
                        player.Stats.GamesPlayed++;
                    
                        // Fix: Pass both player ID and result to UpdatePlayerStatsAsync
                        await _playerManager.UpdatePlayerStatsAsync(player.Id, result);
                    }
                }
            }
            
            // Save final game state
            await _persistenceService.SaveGameStateAsync(room);
        }

        public GameRoom GetRoom(string roomId)
        {
            _rooms.TryGetValue(roomId, out var room);
            return room;
        }

        public List<GameRoom> GetAllRooms()
        {
            return _rooms.Values.ToList();
        }

        public List<GameRoom> GetAvailableRooms()
        {
            return _rooms.Values
                .Where(r => !r.IsPrivate && r.State == GameState.Waiting && r.Players.Count < r.MaxPlayers)
                .ToList();
        }

        // Add method to restore game states
        public async Task RestoreGameStatesAsync()
        {
            try
            {
                var gameStates = await _persistenceService.LoadAllGameStatesAsync();
                
                foreach (var gameState in gameStates)
                {
                    // Skip games that are too old (e.g., more than 24 hours)
                    if (DateTime.UtcNow - gameState.LastUpdated > TimeSpan.FromHours(24))
                    {
                        await _persistenceService.DeleteGameStateAsync(gameState.RoomId);
                        continue;
                    }
                    
                    // Recreate the game room
                    var room = new GameRoom
                    {
                        Id = gameState.RoomId,
                        Name = gameState.Name,
                        GameType = gameState.GameType,
                        MaxPlayers = gameState.MaxPlayers,
                        IsPrivate = gameState.IsPrivate,
                        Password = gameState.Password,
                        CreatedAt = gameState.CreatedAt,
                        State = gameState.State,
                        Players = gameState.Players,
                        GameData = gameState.GameData,
                        CurrentTurnPlayerId = gameState.CurrentTurnPlayerId
                    };
                    
                    _rooms[room.Id] = room;
                    _logger.LogInformation("Restored game room {RoomId} of type {GameType}", room.Id, room.GameType);
                }
                
                _logger.LogInformation("Restored {Count} game rooms", gameStates.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore game states");
            }
        }

        public async Task<bool> AddSpectatorAsync(string playerId, string roomId)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                _logger.LogWarning("Attempted to add spectator to non-existent room {RoomId}", roomId);
                return false;
            }

            var player = _playerManager.GetPlayer(playerId);
            if (player == null)
            {
                _logger.LogWarning("Attempted to add non-existent player {PlayerId} as spectator", playerId);
                return false;
            }

            // Check if player is already in the room as a player
            if (room.Players.ContainsKey(playerId))
            {
                _logger.LogWarning("Player {PlayerId} is already in room {RoomId} as a player", playerId, roomId);
                return false;
            }

            // Check if player is already a spectator
            if (room.Spectators.ContainsKey(playerId))
            {
                _logger.LogWarning("Player {PlayerId} is already a spectator in room {RoomId}", playerId, roomId);
                return true;
            }

            // Add player as spectator
            room.Spectators[playerId] = player;
            _logger.LogInformation("Player {PlayerId} added as spectator to room {RoomId}", playerId, roomId);

            // Save game state
            await _persistenceService.SaveGameStateAsync(room);

            return true;
        }

        public async Task<bool> RemoveSpectatorAsync(string playerId, string roomId)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                _logger.LogWarning("Attempted to remove spectator from non-existent room {RoomId}", roomId);
                return false;
            }

            // Check if player is a spectator
            if (!room.Spectators.ContainsKey(playerId))
            {
                _logger.LogWarning("Player {PlayerId} is not a spectator in room {RoomId}", playerId, roomId);
                return false;
            }

            // Remove player from spectators
            room.Spectators.Remove(playerId);
            _logger.LogInformation("Player {PlayerId} removed as spectator from room {RoomId}", playerId, roomId);

            // Save game state
            await _persistenceService.SaveGameStateAsync(room);

            return true;
        }

        public List<Player> GetRoomSpectators(string roomId)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                _logger.LogWarning("Attempted to get spectators of non-existent room {RoomId}", roomId);
                return new List<Player>();
            }

            return room.Spectators.Values.ToList();
        }
        
        // Add method to get rooms where a player is participating
        public List<GameRoom> GetPlayerRooms(string playerId)
        {
            return _rooms.Values
                .Where(r => r.Players.ContainsKey(playerId))
                .ToList();
        }

        // Remove the duplicate RestoreGameStatesAsync method that starts here
        // The original method is already defined earlier in the file
    }
}