using GameServer.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Core.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ILogger<AuthController> _logger;
        private readonly UserService _userService;

        public AuthController(ILogger<AuthController> logger, UserService userService)
        {
            _logger = logger;
            _userService = userService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest("Username and password are required");
            }

            var response = await _userService.RegisterAsync(request);
            
            if (!response.Success)
            {
                return BadRequest(response.Message);
            }

            return Ok(new
            {
                response.PlayerId,
                response.Username,
                Token = response.Token
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest("Username and password are required");
            }

            var response = await _userService.LoginAsync(request);
            
            if (!response.Success)
            {
                return Unauthorized(response.Message);
            }

            return Ok(new
            {
                response.PlayerId,
                response.Username,
                Token = response.Token
            });
        }
    }
}

public class RegisterRequest
{
    public string Username { get; set; }
    public string Password { get; set; }
}

public class LoginRequest
{
    public string Username { get; set; }
    public string Password { get; set; }
}