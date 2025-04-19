using System.Security.Claims;
using GameServer.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Core.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GameController : ControllerBase
    {
        private readonly ILogger<GameController> _logger;
        private readonly GameRoomManager _roomManager;
        private readonly PlayerManager _playerManager;

        public GameController(
            ILogger<GameController> logger,
            GameRoomManager roomManager,
            PlayerManager playerManager)
        {
            _logger = logger;
            _roomManager = roomManager;
            _playerManager = playerManager;
        }

        [HttpGet("rooms")]
        public IActionResult GetAvailableRooms()
        {
            var rooms = _roomManager.GetAvailableRooms();
            return Ok(rooms);
        }

        [HttpGet("rooms/{roomId}")]
        public IActionResult GetRoom(string roomId)
        {
            var room = _roomManager.GetRoom(roomId);
            
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
            var player = await _playerManager.GetPlayerAsync(playerId);
            
            if (player == null)
            {
                return NotFound();
            }

            return Ok(player);
        }

        [HttpGet("players/online")]
        public IActionResult GetOnlinePlayers()
        {
            var players = _playerManager.GetOnlinePlayers();
            return Ok(players);
        }
    }
}