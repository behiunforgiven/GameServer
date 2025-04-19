using GameServer.Common.Models;
using GameServer.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GameServer.Core.Hubs
{
    [Authorize]
    public class GameHub : Hub
    {
        private readonly ILogger<GameHub> _logger;
        private readonly GameRoomManager _roomManager;
        private readonly PlayerManager _playerManager;
        private readonly MatchmakingService _matchmakingService;
        private readonly AnalyticsService _analyticsService;
        private readonly Dictionary<string, DateTime> _playerLoginTimes;

        public GameHub(
            ILogger<GameHub> logger,
            PlayerManager playerManager,
            GameRoomManager roomManager,
            MatchmakingService matchmakingService,
            AnalyticsService analyticsService)
        {
            _logger = logger;
            _playerManager = playerManager;
            _roomManager = roomManager;
            // Remove the _extensionManager reference since it's not in the constructor parameters
            _matchmakingService = matchmakingService;
            _analyticsService = analyticsService;
            _playerLoginTimes = new Dictionary<string, DateTime>();
        }

        public override async Task OnConnectedAsync()
        {
            var playerId = Context.UserIdentifier;
            _logger.LogInformation("Player connected: {PlayerId}", playerId);
            
            // Track player login
            _playerLoginTimes[playerId] = DateTime.UtcNow;
            _analyticsService.TrackPlayerLogin(playerId);
            
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var playerId = Context.UserIdentifier;
            _logger.LogInformation("Player disconnected: {PlayerId}", playerId);
            
            // Track player logout
            if (_playerLoginTimes.TryGetValue(playerId, out var loginTime))
            {
                var sessionDuration = DateTime.UtcNow - loginTime;
                _analyticsService.TrackPlayerLogout(playerId, sessionDuration);
                _playerLoginTimes.Remove(playerId);
            }
            
            // Remove from matchmaking queue if present
            await _matchmakingService.RemoveFromMatchmakingQueueAsync(playerId);
            
            // Leave any rooms
            var rooms = _roomManager.GetPlayerRooms(playerId);
            foreach (var room in rooms)
            {
                await LeaveRoom(room.Id);
            }
            
            await base.OnDisconnectedAsync(exception);
        }

        public async Task GetAvailableRooms()
        {
            var rooms = _roomManager.GetAvailableRooms();
            await Clients.Caller.SendAsync("AvailableRooms", rooms);
        }

        public async Task<string> CreateRoom(string name, string gameType, int maxPlayers, bool isPrivate, string password = null)
        {
            var playerId = Context.UserIdentifier;
            // Fix the CreateRoomAsync call to match the expected parameters
            var room = await _roomManager.CreateRoomAsync(
                name, 
                gameType, 
                maxPlayers, 
                isPrivate, 
                password);
            
            if (room != null)
            {
                // Join the room after creating it
                await _roomManager.JoinRoomAsync(room.Id, await _playerManager.GetPlayerAsync(playerId), password);
                
                await Groups.AddToGroupAsync(Context.ConnectionId, room.Id);
                await Clients.Caller.SendAsync("RoomCreated", room);
                
                // Track game creation for analytics
                _analyticsService.TrackGameCreated(gameType);
                
                return room.Id;
            }
            
            return null;
        }

        public async Task<bool> JoinRoom(string roomId, string password = null)
        {
            var playerId = Context.UserIdentifier;
            var player = await _playerManager.GetPlayerAsync(playerId);
            
            if (player == null)
            {
                return false;
            }
            
            var success = await _roomManager.JoinRoomAsync(roomId, player, password);
            
            if (success)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"room_{roomId}");
                var room = _roomManager.GetRoom(roomId);
                await Clients.Group($"room_{roomId}").SendAsync("PlayerJoined", player);
                await Clients.Caller.SendAsync("RoomJoined", room);
            }
            
            return success;
        }

        public async Task<bool> LeaveRoom(string roomId)
        {
            var playerId = Context.UserIdentifier;
            var success = await _roomManager.LeaveRoomAsync(roomId, playerId);
            
            if (success)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"room_{roomId}");
                await Clients.Group($"room_{roomId}").SendAsync("PlayerLeft", playerId);
                await Clients.Caller.SendAsync("RoomLeft", roomId);
            }
            
            return success;
        }

        public async Task<bool> MakeMove(GameMove move)
        {
            var playerId = Context.UserIdentifier;
            move.PlayerId = playerId;
            
            var moveResult = await _roomManager.ProcessMoveAsync(move);
            
            if (moveResult)
            {
                var room = _roomManager.GetRoom(move.RoomId);
                await Clients.Group(move.RoomId).SendAsync("MoveMade", move);
                
                // Track move for analytics
                _analyticsService.TrackMove(room.GameType, playerId);
                
                // Check if the game is over
                // Use the room's state to check if game is complete
                var isGameOver = room.State == GameState.Finished;
                
                if (isGameOver)
                {
                    // Create a dictionary for game results
                    var gameResults = new Dictionary<string, GameResult>();
                    // Fix: EndGameAsync doesn't return a value, so don't assign it
                    await _roomManager.EndGameAsync(move.RoomId, GameState.Finished, gameResults);
                    await Clients.Group(move.RoomId).SendAsync("GameEnded", gameResults);
                    
                    // Track game end for analytics
                    var gameDuration = DateTime.UtcNow - room.CreatedAt;
                    _analyticsService.TrackGameEnded(room.GameType, gameDuration);
                    
                    // Track rating changes
                    foreach (var player in room.Players.Keys)
                    {
                        if (gameResults.TryGetValue(player, out var playerResult))
                        {
                            double ratingChange = 0;
                            switch (playerResult.Outcome)
                            {
                                case GameOutcome.Win:
                                    ratingChange = 25;
                                    break;
                                case GameOutcome.Loss:
                                    ratingChange = -15;
                                    break;
                                case GameOutcome.Draw:
                                    ratingChange = 5;
                                    break;
                            }
                            
                            _analyticsService.TrackRatingChange(player, ratingChange);
                        }
                    }
                }
                else
                {
                    // Update turn - use the current turn player ID from the room
                    await Clients.Group(move.RoomId).SendAsync("TurnChanged", room.CurrentTurnPlayerId);
                }
            }
            
            return moveResult;
        }

        public async Task<bool> JoinAsSpectator(string roomId)
        {
            var playerId = Context.UserIdentifier;
            var result = await _roomManager.AddSpectatorAsync(playerId, roomId);
            
            if (result)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
                
                var room = _roomManager.GetRoom(roomId);
                var spectators = _roomManager.GetRoomSpectators(roomId);
                
                // Notify room that a spectator joined
                await Clients.Group(roomId).SendAsync("SpectatorJoined", playerId, room.Players[playerId].Username);
                
                // Send current game state to the spectator
                await Clients.Caller.SendAsync("GameState", new
                {
                    Room = room,
                    Players = room.Players.Values.ToList(),
                    Spectators = spectators,
                    CurrentTurn = room.CurrentTurnPlayerId,
                    GameData = room.GameData
                });
            }
            
            return result;
        }

        public async Task<bool> LeaveSpectating(string roomId)
        {
            var playerId = Context.UserIdentifier;
            var result = await _roomManager.RemoveSpectatorAsync(playerId, roomId);
            
            if (result)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
                
                // Notify room that a spectator left
                await Clients.Group(roomId).SendAsync("SpectatorLeft", playerId);
            }
            
            return result;
        }

        public async Task<List<Player>> GetRoomSpectators(string roomId)
        {
            return _roomManager.GetRoomSpectators(roomId);
        }

        public async Task SendChatMessage(string roomId, string message)
        {
            var playerId = Context.UserIdentifier;
            var player = await _playerManager.GetPlayerAsync(playerId);
            
            if (player != null)
            {
                var chatMessage = new
                {
                    PlayerId = playerId,
                    PlayerName = player.Username,
                    Message = message,
                    Timestamp = DateTime.UtcNow
                };
                
                await Clients.Group($"room_{roomId}").SendAsync("ChatMessage", chatMessage);
            }
        }

        // Add new methods for matchmaking

        public async Task JoinMatchmaking(string gameType, int? desiredRating = null)
        {
            var playerId = Context.UserIdentifier;
            await _matchmakingService.AddToMatchmakingQueueAsync(playerId, gameType, desiredRating);
            await Clients.Caller.SendAsync("JoinedMatchmaking", gameType);
        }

        public async Task LeaveMatchmaking()
        {
            var playerId = Context.UserIdentifier;
            await _matchmakingService.RemoveFromMatchmakingQueueAsync(playerId);
            await Clients.Caller.SendAsync("LeftMatchmaking");
        }
    }
}