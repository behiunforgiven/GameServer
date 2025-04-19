using System.Collections.Concurrent;

namespace GameServer.Core.Services
{
    public class MatchmakingService : BackgroundService
    {
        private readonly ILogger<MatchmakingService> _logger;
        private readonly PlayerManager _playerManager;
        private readonly GameRoomManager _roomManager;
        private readonly ConcurrentDictionary<string, MatchmakingRequest> _matchmakingQueue;
        private readonly TimeSpan _matchmakingInterval = TimeSpan.FromSeconds(5);

        public MatchmakingService(
            ILogger<MatchmakingService> logger,
            PlayerManager playerManager,
            GameRoomManager roomManager)
        {
            _logger = logger;
            _playerManager = playerManager;
            _roomManager = roomManager;
            _matchmakingQueue = new ConcurrentDictionary<string, MatchmakingRequest>();
        }

        public Task AddToMatchmakingQueueAsync(string playerId, string gameType, int? desiredRating = null)
        {
            var player = _playerManager.GetAllPlayers().FirstOrDefault(p => p.Id == playerId);
            if (player == null)
            {
                _logger.LogWarning("Attempted to add non-existent player {PlayerId} to matchmaking queue", playerId);
                return Task.CompletedTask;
            }

            var request = new MatchmakingRequest
            {
                PlayerId = playerId,
                PlayerRating = player.Stats.Rating,
                GameType = gameType,
                DesiredRating = desiredRating,
                JoinedAt = DateTime.UtcNow
            };

            _matchmakingQueue.AddOrUpdate(playerId, request, (_, _) => request);
            _logger.LogInformation("Added player {PlayerId} to matchmaking queue for {GameType}", playerId, gameType);
            
            return Task.CompletedTask;
        }

        public Task RemoveFromMatchmakingQueueAsync(string playerId)
        {
            if (_matchmakingQueue.TryRemove(playerId, out _))
            {
                _logger.LogInformation("Removed player {PlayerId} from matchmaking queue", playerId);
            }
            
            return Task.CompletedTask;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Matchmaking service is starting");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessMatchmakingQueueAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing matchmaking queue");
                }

                await Task.Delay(_matchmakingInterval, stoppingToken);
            }

            _logger.LogInformation("Matchmaking service is stopping");
        }

        private async Task ProcessMatchmakingQueueAsync()
        {
            // Group players by game type
            var gameTypeGroups = _matchmakingQueue.Values
                .GroupBy(r => r.GameType)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var gameType in gameTypeGroups.Keys)
            {
                var requests = gameTypeGroups[gameType];
                
                // Sort by time in queue (oldest first)
                requests.Sort((a, b) => a.JoinedAt.CompareTo(b.JoinedAt));
                
                // Process each request
                for (int i = 0; i < requests.Count; i++)
                {
                    var request = requests[i];
                    
                    // Skip if player is already matched
                    if (request.IsMatched)
                    {
                        continue;
                    }
                    
                    // Find a suitable match
                    MatchmakingRequest bestMatch = null;
                    int bestMatchScore = int.MaxValue;
                    
                    for (int j = i + 1; j < requests.Count; j++)
                    {
                        var candidate = requests[j];
                        
                        if (candidate.IsMatched)
                        {
                            continue;
                        }
                        
                        // Calculate match score (lower is better)
                        int ratingDifference = Math.Abs(request.PlayerRating - candidate.PlayerRating);
                        int waitTimeBonus = (int)Math.Min(300, (DateTime.UtcNow - request.JoinedAt).TotalSeconds);
                        int matchScore = ratingDifference - waitTimeBonus;
                        
                        // Check if this is the best match so far
                        if (matchScore < bestMatchScore)
                        {
                            bestMatch = candidate;
                            bestMatchScore = matchScore;
                        }
                    }
                    
                    // If we found a match, create a game room
                    if (bestMatch != null)
                    {
                        request.IsMatched = true;
                        bestMatch.IsMatched = true;
                        
                        await CreateMatchedGameAsync(request, bestMatch);
                    }
                    else if ((DateTime.UtcNow - request.JoinedAt).TotalMinutes >= 2)
                    {
                        // If player has been waiting for more than 2 minutes, match with anyone
                        var anyUnmatched = requests.FirstOrDefault(r => !r.IsMatched && r.PlayerId != request.PlayerId);
                        
                        if (anyUnmatched != null)
                        {
                            request.IsMatched = true;
                            anyUnmatched.IsMatched = true;
                            
                            await CreateMatchedGameAsync(request, anyUnmatched);
                        }
                    }
                }
            }
            
            // Remove matched players from the queue
            foreach (var playerId in _matchmakingQueue.Keys)
            {
                if (_matchmakingQueue.TryGetValue(playerId, out var request) && request.IsMatched)
                {
                    _matchmakingQueue.TryRemove(playerId, out _);
                }
            }
        }

        private async Task CreateMatchedGameAsync(MatchmakingRequest player1, MatchmakingRequest player2)
        {
            try
            {
                var roomName = $"Matched Game - {Guid.NewGuid().ToString().Substring(0, 8)}";
                // Fix the parameter order to match GameRoomManager.CreateRoomAsync signature
                var room = await _roomManager.CreateRoomAsync(
                    roomName,
                    player1.GameType,
                    2,
                    false,
                    null);
                
                if (room == null)
                {
                    _logger.LogError("Failed to create room for matched players {Player1} and {Player2}",
                        player1.PlayerId, player2.PlayerId);
                    return;
                }
                
                // Add the second player to the room
                await _roomManager.JoinRoomAsync(room.Id, await _playerManager.GetPlayerAsync(player2.PlayerId));
                
                _logger.LogInformation("Created matched game room {RoomId} for players {Player1} and {Player2}",
                    room.Id, player1.PlayerId, player2.PlayerId);
                
                // Start the game
                await _roomManager.StartGameAsync(room.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating matched game for players {Player1} and {Player2}",
                    player1.PlayerId, player2.PlayerId);
            }
        }
    }

    public class MatchmakingRequest
    {
        public string PlayerId { get; set; }
        public int PlayerRating { get; set; }
        public string GameType { get; set; }
        public int? DesiredRating { get; set; }
        public DateTime JoinedAt { get; set; }
        public bool IsMatched { get; set; }
    }
}