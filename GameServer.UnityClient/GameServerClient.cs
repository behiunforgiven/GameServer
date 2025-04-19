using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace GameServer.UnityClient
{
    public class GameServerClient : MonoBehaviour
    {
        [SerializeField] private string serverUrl = "https://localhost:5001";
        
        private HubConnection _hubConnection;
        private string _authToken;
        private string _playerId;
        private string _currentRoomId;
        
        // Events
        public event Action<List<GameRoom>> OnAvailableRoomsReceived;
        public event Action<GameRoom> OnRoomCreated;
        public event Action<GameRoom> OnRoomJoined;
        public event Action<string> OnRoomLeft;
        public event Action<Player> OnPlayerJoined;
        public event Action<string> OnPlayerLeft;
        public event Action<GameMove> OnMoveMade;
        public event Action<string> OnTurnChanged;
        public event Action<Dictionary<string, GameResult>> OnGameEnded;
        public event Action<ChatMessage> OnChatMessageReceived;
        
        // Connection state
        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
        public string PlayerId => _playerId;
        public string CurrentRoomId => _currentRoomId;
        
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
        
        public async Task<bool> ConnectAsync(string authToken)
        {
            if (IsConnected)
            {
                return true;
            }
            
            _authToken = authToken;
            
            try
            {
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl($"{serverUrl}/gamehub", options =>
                    {
                        options.AccessTokenProvider = () => Task.FromResult(_authToken);
                    })
                    .WithAutomaticReconnect()
                    .Build();
                
                RegisterHubHandlers();
                
                await _hubConnection.StartAsync();
                Debug.Log("Connected to game server");
                
                // Get player info
                var playerInfo = await GetPlayerInfoAsync();
                _playerId = playerInfo.Id;
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to connect to game server: {ex.Message}");
                return false;
            }
        }
        
        public async Task DisconnectAsync()
        {
            if (_hubConnection != null)
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
                _authToken = null;
                _playerId = null;
                _currentRoomId = null;
                
                Debug.Log("Disconnected from game server");
            }
        }
        
        public async Task<Player> GetPlayerInfoAsync()
        {
            using var request = UnityWebRequest.Get($"{serverUrl}/api/game/player");
            request.SetRequestHeader("Authorization", $"Bearer {_authToken}");
            
            var operation = await request.SendWebRequest();
            
            if (operation.result == UnityWebRequest.Result.Success)
            {
                var json = request.downloadHandler.text;
                return JsonConvert.DeserializeObject<Player>(json);
            }
            
            Debug.LogError($"Failed to get player info: {request.error}");
            return null;
        }
        
        public async Task GetAvailableRoomsAsync()
        {
            if (!IsConnected) return;
            await _hubConnection.InvokeAsync("GetAvailableRooms");
        }
        
        public async Task<string> CreateRoomAsync(string name, string gameType, int maxPlayers, bool isPrivate, string password = null)
        {
            if (!IsConnected) return null;
            
            var roomId = await _hubConnection.InvokeAsync<string>(
                "CreateRoom", name, gameType, maxPlayers, isPrivate, password);
            
            if (!string.IsNullOrEmpty(roomId))
            {
                _currentRoomId = roomId;
            }
            
            return roomId;
        }
        
        public async Task<bool> JoinRoomAsync(string roomId, string password = null)
        {
            if (!IsConnected) return false;
            
            var success = await _hubConnection.InvokeAsync<bool>("JoinRoom", roomId, password);
            
            if (success)
            {
                _currentRoomId = roomId;
            }
            
            return success;
        }
        
        public async Task<bool> LeaveRoomAsync()
        {
            if (!IsConnected || string.IsNullOrEmpty(_currentRoomId)) return false;
            
            var success = await _hubConnection.InvokeAsync<bool>("LeaveRoom", _currentRoomId);
            
            if (success)
            {
                _currentRoomId = null;
            }
            
            return success;
        }
        
        public async Task<bool> MakeMoveAsync<T>(T moveData) where T : class
        {
            if (!IsConnected || string.IsNullOrEmpty(_currentRoomId)) return false;
            
            var move = new GameMove
            {
                RoomId = _currentRoomId,
                MoveData = moveData
            };
            
            return await _hubConnection.InvokeAsync<bool>("MakeMove", move);
        }
        
        public async Task JoinAsSpectatorAsync(string roomId)
        {
            if (!IsConnected) return;
            await _hubConnection.InvokeAsync("JoinAsSpectator", roomId);
        }
        
        public async Task SendChatMessageAsync(string message)
        {
            if (!IsConnected || string.IsNullOrEmpty(_currentRoomId)) return;
            await _hubConnection.InvokeAsync("SendChatMessage", _currentRoomId, message);
        }
        
        private void RegisterHubHandlers()
        {
            _hubConnection.On<List<GameRoom>>("AvailableRooms", rooms =>
            {
                OnAvailableRoomsReceived?.Invoke(rooms);
            });
            
            _hubConnection.On<GameRoom>("RoomCreated", room =>
            {
                OnRoomCreated?.Invoke(room);
            });
            
            _hubConnection.On<GameRoom>("RoomJoined", room =>
            {
                OnRoomJoined?.Invoke(room);
            });
            
            _hubConnection.On<string>("RoomLeft", roomId =>
            {
                OnRoomLeft?.Invoke(roomId);
            });
            
            _hubConnection.On<Player>("PlayerJoined", player =>
            {
                OnPlayerJoined?.Invoke(player);
            });
            
            _hubConnection.On<string>("PlayerLeft", playerId =>
            {
                OnPlayerLeft?.Invoke(playerId);
            });
            
            _hubConnection.On<GameMove>("MoveMade", move =>
            {
                OnMoveMade?.Invoke(move);
            });
            
            _hubConnection.On<string>("TurnChanged", playerId =>
            {
                OnTurnChanged?.Invoke(playerId);
            });
            
            _hubConnection.On<Dictionary<string, GameResult>>("GameEnded", results =>
            {
                OnGameEnded?.Invoke(results);
            });
            
            _hubConnection.On<ChatMessage>("ChatMessage", message =>
            {
                OnChatMessageReceived?.Invoke(message);
            });
        }
    }

    [Serializable]
    public class Player
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public bool IsConnected { get; set; }
        public PlayerStats Stats { get; set; }
    }

    [Serializable]
    public class PlayerStats
    {
        public int GamesPlayed { get; set; }
        public int GamesWon { get; set; }
        public int GamesLost { get; set; }
        public int GamesTied { get; set; }
        public int Rating { get; set; }
    }

    [Serializable]
    public class GameRoom
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string GameType { get; set; }
        public Dictionary<string, Player> Players { get; set; }
        public List<string> Spectators { get; set; }
        public string State { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public int MaxPlayers { get; set; }
        public bool IsPrivate { get; set; }
        public string CurrentTurnPlayerId { get; set; }
        public object GameData { get; set; }
    }
    }