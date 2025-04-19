using System.Collections.Concurrent;
using GameServer.Core.Data;

namespace GameServer.Core.Services
{
    public class AnalyticsService : BackgroundService
    {
        private readonly ILogger<AnalyticsService> _logger;
        private readonly GameDbContext _dbContext;
        private readonly ConcurrentQueue<AnalyticsEvent> _eventQueue;
        private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(30);
        
        // Metrics
        private readonly ConcurrentDictionary<string, int> _gameTypePlayCount;
        private readonly ConcurrentDictionary<string, TimeSpan> _averageGameDuration;
        private readonly ConcurrentDictionary<string, int> _playerLoginCount;
        private readonly ConcurrentDictionary<string, int> _moveCount;
        private readonly ConcurrentDictionary<string, List<double>> _playerRatingChanges;

        public AnalyticsService(ILogger<AnalyticsService> logger, GameDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
            _eventQueue = new ConcurrentQueue<AnalyticsEvent>();
            
            _gameTypePlayCount = new ConcurrentDictionary<string, int>();
            _averageGameDuration = new ConcurrentDictionary<string, TimeSpan>();
            _playerLoginCount = new ConcurrentDictionary<string, int>();
            _moveCount = new ConcurrentDictionary<string, int>();
            _playerRatingChanges = new ConcurrentDictionary<string, List<double>>();
        }

        public void TrackGameCreated(string gameType)
        {
            _eventQueue.Enqueue(new AnalyticsEvent
            {
                Type = AnalyticsEventType.GameCreated,
                GameType = gameType,
                Timestamp = DateTime.UtcNow
            });
            
            _gameTypePlayCount.AddOrUpdate(gameType, 1, (_, count) => count + 1);
        }

        public void TrackGameEnded(string gameType, TimeSpan duration)
        {
            _eventQueue.Enqueue(new AnalyticsEvent
            {
                Type = AnalyticsEventType.GameEnded,
                GameType = gameType,
                Duration = duration,
                Timestamp = DateTime.UtcNow
            });
            
            // Update average game duration
            _averageGameDuration.AddOrUpdate(
                gameType,
                duration,
                (_, currentAvg) => TimeSpan.FromTicks((currentAvg.Ticks + duration.Ticks) / 2));
        }

        public void TrackPlayerLogin(string playerId)
        {
            _eventQueue.Enqueue(new AnalyticsEvent
            {
                Type = AnalyticsEventType.PlayerLogin,
                PlayerId = playerId,
                Timestamp = DateTime.UtcNow
            });
            
            _playerLoginCount.AddOrUpdate(playerId, 1, (_, count) => count + 1);
        }

        public void TrackPlayerLogout(string playerId, TimeSpan sessionDuration)
        {
            _eventQueue.Enqueue(new AnalyticsEvent
            {
                Type = AnalyticsEventType.PlayerLogout,
                PlayerId = playerId,
                Duration = sessionDuration,
                Timestamp = DateTime.UtcNow
            });
        }

        public void TrackMove(string gameType, string playerId)
        {
            _eventQueue.Enqueue(new AnalyticsEvent
            {
                Type = AnalyticsEventType.Move,
                GameType = gameType,
                PlayerId = playerId,
                Timestamp = DateTime.UtcNow
            });
            
            _moveCount.AddOrUpdate(gameType, 1, (_, count) => count + 1);
        }

        public void TrackRatingChange(string playerId, double ratingChange)
        {
            _eventQueue.Enqueue(new AnalyticsEvent
            {
                Type = AnalyticsEventType.RatingChange,
                PlayerId = playerId,
                Value = ratingChange,
                Timestamp = DateTime.UtcNow
            });
            
            _playerRatingChanges.AddOrUpdate(
                playerId,
                new List<double> { ratingChange },
                (_, changes) => { changes.Add(ratingChange); return changes; });
        }

        public Dictionary<string, object> GetCurrentMetrics()
        {
            return new Dictionary<string, object>
            {
                ["GameTypePlayCount"] = _gameTypePlayCount.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                ["AverageGameDuration"] = _averageGameDuration.ToDictionary(
                    kvp => kvp.Key, 
                    kvp => kvp.Value.TotalMinutes),
                ["TopPlayers"] = _playerRatingChanges
                    .OrderByDescending(kvp => kvp.Value.Sum())
                    .Take(10)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Sum()),
                ["MostPopularGameTypes"] = _gameTypePlayCount
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(5)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                ["TotalMoves"] = _moveCount.Values.Sum(),
                ["ActivePlayers"] = _playerLoginCount.Count
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Analytics service is starting");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessEventQueueAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing analytics events");
                }

                await Task.Delay(_processingInterval, stoppingToken);
            }

            _logger.LogInformation("Analytics service is stopping");
        }

        private async Task ProcessEventQueueAsync()
        {
            var events = new List<AnalyticsEvent>();
            
            // Dequeue all events
            while (_eventQueue.TryDequeue(out var analyticsEvent))
            {
                events.Add(analyticsEvent);
            }
            
            if (events.Count == 0)
            {
                return;
            }
            
            _logger.LogInformation("Processing {Count} analytics events", events.Count);
            
            try
            {
                // Process events in batches
                foreach (var batch in events.Chunk(100))
                {
                    // In a real implementation, you would store these events in a database
                    // or send them to an analytics service
                    
                    // For now, we'll just log some summary information
                    var gameCreatedCount = batch.Count(e => e.Type == AnalyticsEventType.GameCreated);
                    var gameEndedCount = batch.Count(e => e.Type == AnalyticsEventType.GameEnded);
                    var playerLoginCount = batch.Count(e => e.Type == AnalyticsEventType.PlayerLogin);
                    var playerLogoutCount = batch.Count(e => e.Type == AnalyticsEventType.PlayerLogout);
                    var moveCount = batch.Count(e => e.Type == AnalyticsEventType.Move);
                    
                    _logger.LogInformation(
                        "Processed batch: {GameCreated} games created, {GameEnded} games ended, " +
                        "{PlayerLogin} player logins, {PlayerLogout} player logouts, {MoveCount} moves",
                        gameCreatedCount, gameEndedCount, playerLoginCount, playerLogoutCount, moveCount);
                    
                    // Store analytics data in the database
                    await StoreAnalyticsDataAsync(batch);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing analytics events");
            }
        }

        private async Task StoreAnalyticsDataAsync(IEnumerable<AnalyticsEvent> events)
        {
            // In a real implementation, you would store these events in a database table
            // For this example, we'll just log them
            foreach (var analyticsEvent in events)
            {
                _logger.LogDebug("Analytics event: {Type}, {GameType}, {PlayerId}, {Timestamp}",
                    analyticsEvent.Type, analyticsEvent.GameType, analyticsEvent.PlayerId, analyticsEvent.Timestamp);
            }
            
            await Task.CompletedTask;
        }
    }

    public enum AnalyticsEventType
    {
        GameCreated,
        GameEnded,
        PlayerLogin,
        PlayerLogout,
        Move,
        RatingChange
    }

    public class AnalyticsEvent
    {
        public AnalyticsEventType Type { get; set; }
        public string GameType { get; set; }
        public string PlayerId { get; set; }
        public TimeSpan? Duration { get; set; }
        public double? Value { get; set; }
        public DateTime Timestamp { get; set; }
    }
}