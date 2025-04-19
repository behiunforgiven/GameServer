using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GameServer.Core.Data;
using GameServer.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using UserAccountEntity = GameServer.Core.Data.UserAccount;
using PlayerStatisticsEntity = GameServer.Core.Data.PlayerStatistics;

namespace GameServer.Core.Services
{
    public class UserService
    {
        private readonly ILogger<UserService> _logger;
        private readonly GameDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly PlayerManager _playerManager;

        public UserService(
            ILogger<UserService> logger,
            GameDbContext dbContext,
            IConfiguration configuration,
            PlayerManager playerManager)
        {
            _logger = logger;
            _dbContext = dbContext;
            _configuration = configuration;
            _playerManager = playerManager;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            try
            {
                // Check if username already exists
                var existingUser = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Username == request.Username);

                if (existingUser != null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Username already exists"
                    };
                }

                // Create new user - use the Data namespace version for the DbContext
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                var user = new UserAccountEntity
                {
                    Username = request.Username,
                    PasswordHash = passwordHash,
                    CreatedAt = DateTime.UtcNow,
                    LastLogin = DateTime.UtcNow,
                    IsAdmin = false,
                    IsBanned = false,
                    Statistics = new PlayerStatisticsEntity
                    {
                        GamesPlayed = 0,
                        GamesWon = 0,     
                        GamesLost = 0,    
                        GamesTied = 0,    
                        Rating = 1000
                    }
                };

                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();

                // Create player
                var player = await _playerManager.CreatePlayerAsync(request.Username);

                // Generate token
                var token = GenerateJwtToken(user.Id.ToString(), user.Username, player.Id);

                return new AuthResponse
                {
                    Success = true,
                    Token = token,
                    Username = user.Username,
                    PlayerId = player.Id
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                return new AuthResponse
                {
                    Success = false,
                    Message = "An error occurred during registration"
                };
            }
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                // Find user by username
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Username == request.Username);

                if (user == null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Invalid username or password"
                    };
                }

                // Check if user is banned
                if (user.IsBanned)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Your account has been banned"
                    };
                }

                // Verify password
                if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Invalid username or password"
                    };
                }

                // Update last login
                user.LastLogin = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                // Get or create player
                var player = await _playerManager.GetPlayerByUsernameAsync(user.Username);
                if (player == null)
                {
                    player = await _playerManager.CreatePlayerAsync(user.Username);
                }

                // Generate token
                var token = GenerateJwtToken(user.Id.ToString(), user.Username, player.Id);

                return new AuthResponse
                {
                    Success = true,
                    Token = token,
                    Username = user.Username,
                    PlayerId = player.Id
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user login");
                return new AuthResponse
                {
                    Success = false,
                    Message = "An error occurred during login"
                };
            }
        }

        private string GenerateJwtToken(string userId, string username, string playerId)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Secret"]);
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim(ClaimTypes.Name, username),
                    new Claim("PlayerId", playerId)
                }),
                Expires = DateTime.UtcNow.AddMinutes(Convert.ToDouble(_configuration["Jwt:ExpiryMinutes"])),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"]
            };
            
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}