using GameServer.Core.Data;
using GameServer.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Core.Controllers
{
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AnalyticsController : ControllerBase
    {
        private readonly ILogger<AnalyticsController> _logger;
        private readonly AnalyticsService _analyticsService;
        private readonly GameDbContext _dbContext;

        public AnalyticsController(
            ILogger<AnalyticsController> logger,
            AnalyticsService analyticsService,
            GameDbContext dbContext)
        {
            _logger = logger;
            _analyticsService = analyticsService;
            _dbContext = dbContext;
        }

        [HttpGet("metrics")]
        public IActionResult GetCurrentMetrics()
        {
            var metrics = _analyticsService.GetCurrentMetrics();
            return Ok(metrics);
        }

        [HttpGet("game-history")]
        public async Task<IActionResult> GetGameHistory([FromQuery] int days = 7)
        {
            var startDate = DateTime.UtcNow.AddDays(-days);
            
            var gameHistory = await _dbContext.GameHistories
                .Where(gh => gh.EndTime >= startDate)
                .GroupBy(gh => new { Date = gh.EndTime.Date, gh.GameType })
                .Select(g => new
                {
                    Date = g.Key.Date,
                    GameType = g.Key.GameType,
                    Count = g.Count(),
                    AverageDuration = g.Average(gh => (gh.EndTime - gh.StartTime).TotalMinutes)
                })
                .OrderBy(r => r.Date)
                .ToListAsync();
            
            return Ok(gameHistory);
        }

        [HttpGet("player-activity")]
        public async Task<IActionResult> GetPlayerActivity([FromQuery] int days = 7)
        {
            var startDate = DateTime.UtcNow.AddDays(-days);
            
            // In a real implementation, you would track player logins in a separate table
            // For this example, we'll use the last login time from the user accounts
            var playerActivity = await _dbContext.Users
                .Where(u => u.LastLogin >= startDate)
                .GroupBy(u => u.LastLogin.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Count = g.Count()
                })
                .OrderBy(r => r.Date)
                .ToListAsync();
            
            return Ok(playerActivity);
        }

        [HttpGet("top-players")]
        public async Task<IActionResult> GetTopPlayers([FromQuery] int count = 10)
        {
            var topPlayers = await _dbContext.PlayerStatistics
                .Include(ps => ps.User)
                .OrderByDescending(ps => ps.Rating)
                .Take(count)
                .Select(ps => new
                {
                    Username = ps.User.Username,
                    Rating = ps.Rating,
                    GamesPlayed = ps.GamesPlayed,
                    WinRate = ps.GamesPlayed > 0 
                        ? (double)ps.GamesWon / ps.GamesPlayed 
                        : 0
                })
                .ToListAsync();
            
            return Ok(topPlayers);
        }

        [HttpGet("game-type-popularity")]
        public async Task<IActionResult> GetGameTypePopularity([FromQuery] int days = 30)
        {
            var startDate = DateTime.UtcNow.AddDays(-days);
            
            var gameTypePopularity = await _dbContext.GameHistories
                .Where(gh => gh.StartTime >= startDate)
                .GroupBy(gh => gh.GameType)
                .Select(g => new
                {
                    GameType = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(r => r.Count)
                .ToListAsync();
            
            return Ok(gameTypePopularity);
        }
    }
}