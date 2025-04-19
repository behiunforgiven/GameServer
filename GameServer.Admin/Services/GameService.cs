using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using GameServer.Common.Models;

namespace GameServer.Admin.Services
{
    public interface IGameService
    {
        Task<ServerStats> GetServerStatsAsync();
        Task<List<GameRoom>> GetAllRoomsAsync();
        Task<List<Player>> GetAllPlayersAsync();
        Task<bool> TerminateRoomAsync(string roomId);
        Task<bool> BanPlayerAsync(string playerId);
    }

    public class GameService : IGameService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;

        public GameService(HttpClient httpClient, ILocalStorageService localStorage)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
        }

        private async Task SetAuthHeaderAsync()
        {
            var token = await _localStorage.GetItemAsync<string>("authToken");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public async Task<ServerStats> GetServerStatsAsync()
        {
            await SetAuthHeaderAsync();
            return await _httpClient.GetFromJsonAsync<ServerStats>("api/admin/dashboard/stats");
        }

        public async Task<List<GameRoom>> GetAllRoomsAsync()
        {
            await SetAuthHeaderAsync();
            return await _httpClient.GetFromJsonAsync<List<GameRoom>>("api/admin/dashboard/rooms");
        }

        public async Task<List<Player>> GetAllPlayersAsync()
        {
            await SetAuthHeaderAsync();
            return await _httpClient.GetFromJsonAsync<List<Player>>("api/admin/dashboard/players");
        }

        public async Task<bool> TerminateRoomAsync(string roomId)
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.PostAsync($"api/admin/dashboard/rooms/{roomId}/terminate", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> BanPlayerAsync(string playerId)
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.PostAsync($"api/admin/dashboard/players/{playerId}/ban", null);
            return response.IsSuccessStatusCode;
        }
    }

    public class ServerStats
    {
        public int TotalPlayers { get; set; }
        public int OnlinePlayers { get; set; }
        public int TotalRooms { get; set; }
        public int ActiveRooms { get; set; }
        public int WaitingRooms { get; set; }
        public int FinishedRooms { get; set; }
        public int AbandonedRooms { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}