using System.Security.Claims;
using GameServer.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Core.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GameController(
        ILogger<GameController> logger,
        GameRoomManager roomManager,
        PlayerManager playerManager)
        : ControllerBase
    {
        private readonly ILogger<GameController> _logger = logger;

        [HttpGet("rooms")]
        public IActionResult GetAvailableRooms()
        {
            var rooms = roomManager.GetAvailableRooms();
            return Ok(rooms);
        }

        [HttpGet("rooms/{roomId}")]
        public IActionResult GetRoom(string roomId)
        {
            var room = roomManager.GetRoom(roomId);
            
            if (room == null)
            {
                return NotFound();
            }

            return Ok(room);
        }

        [HttpGet("player")]
        public async Task<IActionResult> GetCurrentPlayer()
        {
            var playerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var player = await playerManager.GetPlayerAsync(playerId);
            
            if (player == null)
            {
                return NotFound();
            }

            return Ok(player);
        }

        [HttpGet("players/online")]
        public IActionResult GetOnlinePlayers()
        {
            var players = playerManager.GetOnlinePlayers();
            return Ok(players);
        }
    }
}