using System;
using System.Linq;
using GameServer.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GameServer.Admin.Controllers
{
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : ControllerBase
    {
        private readonly ILogger<DashboardController> _logger;
        private readonly GameRoomManager _roomManager;
        private readonly PlayerManager _playerManager;

        public DashboardController(
            ILogger<DashboardController> logger,
            GameRoomManager roomManager,
            PlayerManager playerManager)
        {
            _logger = logger;
            _roomManager = roomManager;
            _playerManager = playerManager;
        }

        [HttpGet("stats")]
        public IActionResult GetStats()
        {
            var rooms = _roomManager.GetAllRooms();
            var players = _playerManager.GetAllPlayers();
            
            var stats = new
            {
                TotalPlayers = players.Count,
                OnlinePlayers = players.Count(p => p.IsConnected),
                TotalRooms = rooms.Count,
                ActiveRooms = rooms.Count(r => r.State == GameServer.Common.Models.GameState.Playing),
                WaitingRooms = rooms.Count(r => r.State == GameServer.Common.Models.GameState.Waiting),
                FinishedRooms = rooms.Count(r => r.State == GameServer.Common.Models.GameState.Finished),
                AbandonedRooms = rooms.Count(r => r.State == GameServer.Common.Models.GameState.Abandoned),
                LastUpdated = DateTime.UtcNow
            };
            
            return Ok(stats);
        }

        [HttpGet("rooms")]
        public IActionResult GetAllRooms()
        {
            var rooms = _roomManager.GetAllRooms();
            return Ok(rooms);
        }

        [HttpGet("players")]
        public IActionResult GetAllPlayers()
        {
            var players = _playerManager.GetAllPlayers();
            return Ok(players);
        }

        [HttpPost("rooms/{roomId}/terminate")]
        public async Task<IActionResult> TerminateRoom(string roomId)
        {
            var room = _roomManager.GetRoom(roomId);
            
            if (room == null)
            {
                return NotFound();
            }

            await _roomManager.EndGameAsync(roomId, GameServer.Common.Models.GameState.Abandoned);
            _logger.LogInformation("Admin terminated room: {RoomId}", roomId);
            
            return Ok();
        }

        [HttpPost("players/{playerId}/ban")]
        public async Task<IActionResult> BanPlayer(string playerId)
        {
            var player = await _playerManager.GetPlayerAsync(playerId);
            
            if (player == null)
            {
                return NotFound();
            }

            // In a real implementation, you would mark the player as banned in the database
            // and prevent them from logging in or joining games
            
            _logger.LogInformation("Admin banned player: {PlayerId} ({Username})", playerId, player.Username);
            
            return Ok();
        }
    }
}