using System.Text.Json;
using GameServer.Common.Models;
using GameServer.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Core.Services
{
    public class DatabaseGameHistoryService
    {
        private readonly ILogger<DatabaseGameHistoryService> _logger;
        private readonly GameDbContext _dbContext;

        public DatabaseGameHistoryService(ILogger<DatabaseGameHistoryService> logger, GameDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task RecordGameStartAsync(GameRoom room)
        {
            try
            {
                var gameHistory = new GameHistory
                {
                    GameType = room.GameType,
                    StartTime = DateTime.UtcNow,
                    GameState = "Playing",
                    GameData = JsonSerializer.Serialize(room.GameData)
                };

                _dbContext.GameHistories.Add(gameHistory);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Recorded game start for room {RoomId}, history ID: {HistoryId}", 
                    room.Id, gameHistory.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record game start for room {RoomId}", room.Id);
            }
        }

        public async Task RecordGameEndAsync(GameRoom room, Dictionary<string, GameResult> results)
        {
            try
            {
                // Find the most recent game history for this game type that's still in "Playing" state
                var gameHistory = await _dbContext.GameHistories
                    .Where(gh => gh.GameState == "Playing")
                    .OrderByDescending(gh => gh.StartTime)
                    .FirstOrDefaultAsync();

                if (gameHistory == null)
                {
                    _logger.LogWarning("No active game history found for room {RoomId}", room.Id);
                    return;
                }

                gameHistory.EndTime = DateTime.UtcNow;
                gameHistory.GameState = room.State.ToString();
                gameHistory.GameData = JsonSerializer.Serialize(room.GameData);

                // Record player results
                foreach (var playerResult in results)
                {
                    var playerId = playerResult.Key;
                    var result = playerResult.Value;

                    // Get the user account
                    var userAccount = await _dbContext.Users
                        .Include(u => u.Statistics)
                        .FirstOrDefaultAsync(u => u.Username == room.Players[playerId].Username);

                    if (userAccount == null)
                    {
                        _logger.LogWarning("User account not found for player {PlayerId}", playerId);
                        continue;
                    }

                    // Calculate rating change (simple implementation)
                    int scoreChange = 0;
                    switch (result.Outcome)
                    {
                        case GameOutcome.Win:
                            scoreChange = 25;
                            userAccount.Statistics.GamesWon++;
                            break;
                        case GameOutcome.Loss:
                            scoreChange = -15;
                            userAccount.Statistics.GamesLost++;
                            break;
                        case GameOutcome.Draw:
                            scoreChange = 5;
                            userAccount.Statistics.GamesTied++;
                            break;
                    }

                    userAccount.Statistics.GamesPlayed++;
                    userAccount.Statistics.Rating += scoreChange;

                    // Add player result to game history
                    gameHistory.PlayerResults.Add(new PlayerResult
                    {
                        UserId = userAccount.Id,
                        Result = result.Outcome.ToString(),
                        ScoreChange = scoreChange
                    });
                }

                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Recorded game end for room {RoomId}, history ID: {HistoryId}", 
                    room.Id, gameHistory.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record game end for room {RoomId}", room.Id);
            }
        }

        public async Task<List<GameHistory>> GetRecentGamesAsync(int count = 10)
        {
            return await _dbContext.GameHistories
                .Include(gh => gh.PlayerResults)
                .OrderByDescending(gh => gh.EndTime)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<GameHistory>> GetPlayerGamesAsync(int userId, int count = 10)
        {
            return await _dbContext.GameHistories
                .Include(gh => gh.PlayerResults)
                .Where(gh => gh.PlayerResults.Any(pr => pr.UserId == userId))
                .OrderByDescending(gh => gh.EndTime)
                .Take(count)
                .ToListAsync();
        }
    }
}