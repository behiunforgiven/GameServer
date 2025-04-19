using System;
using System.Collections.Generic;
using GameServer.Common.Extensions;
using GameServer.Common.Models;

namespace GameServer.Extensions.Examples
{
    public class TicTacToeExtension : IGameExtension
    {
        public string GameType => "TicTacToe";
        public string DisplayName => "Tic Tac Toe";
        public string Description => "Classic 3x3 grid game where players take turns marking X and O";
        public int MinPlayers => 2;
        public int MaxPlayers => 2;

        public object Initialize(int playerCount)
        {
            if (playerCount != 2)
            {
                throw new ArgumentException("Tic Tac Toe requires exactly 2 players");
            }

            // Initialize the game board as a 3x3 array of empty cells
            return new TicTacToeGameData
            {
                Board = new int[3, 3],
                PlayerSymbols = new Dictionary<string, int>(),
                CurrentTurn = null
            };
        }

        public bool IsValidMove(GameMove move, object gameState)
        {
            var gameData = (TicTacToeGameData)gameState;
            var moveData = (TicTacToeMoveData)move.MoveData;

            // Check if it's the player's turn
            if (gameData.CurrentTurn != null && gameData.CurrentTurn != move.PlayerId)
            {
                return false;
            }

            // Check if the move is within bounds
            if (moveData.Row < 0 || moveData.Row > 2 || moveData.Col < 0 || moveData.Col > 2)
            {
                return false;
            }

            // Check if the cell is empty
            if (gameData.Board[moveData.Row, moveData.Col] != 0)
            {
                return false;
            }

            return true;
        }

        public object ExecuteMove(GameMove move, object gameState)
        {
            var gameData = (TicTacToeGameData)gameState;
            var moveData = (TicTacToeMoveData)move.MoveData;

            // If this is the first move, assign symbols to players
            if (gameData.PlayerSymbols.Count == 0)
            {
                gameData.PlayerSymbols[move.PlayerId] = 1; // X
            }
            else if (gameData.PlayerSymbols.Count == 1 && !gameData.PlayerSymbols.ContainsKey(move.PlayerId))
            {
                gameData.PlayerSymbols[move.PlayerId] = 2; // O
            }

            // Place the player's symbol on the board
            int symbol = gameData.PlayerSymbols[move.PlayerId];
            gameData.Board[moveData.Row, moveData.Col] = symbol;

            // Update the current turn to the other player
            foreach (var playerId in gameData.PlayerSymbols.Keys)
            {
                if (playerId != move.PlayerId)
                {
                    gameData.CurrentTurn = playerId;
                    break;
                }
            }

            return gameData;
        }

        public bool IsGameComplete(object gameState)
        {
            var gameData = (TicTacToeGameData)gameState;
            
            // Check for a winner
            if (HasWinner(gameData.Board) != 0)
            {
                return true;
            }

            // Check for a draw (all cells filled)
            bool isDraw = true;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (gameData.Board[i, j] == 0)
                    {
                        isDraw = false;
                        break;
                    }
                }
                if (!isDraw) break;
            }

            return isDraw;
        }

        public Dictionary<string, GameResult> DetermineResults(object gameState, Dictionary<string, Player> players)
        {
            var gameData = (TicTacToeGameData)gameState;
            var results = new Dictionary<string, GameResult>();

            int winner = HasWinner(gameData.Board);
            
            if (winner != 0)
            {
                // Someone won
                foreach (var player in gameData.PlayerSymbols)
                {
                    if (player.Value == winner)
                    {
                        results[player.Key] = new GameResult(player.Key, GameOutcome.Win, 10);
                    }
                    else
                    {
                        results[player.Key] = new GameResult(player.Key, GameOutcome.Loss, -5);
                    }
                }
            }
            else
            {
                // It's a draw
                foreach (var player in gameData.PlayerSymbols)
                {
                    results[player.Key] = new GameResult(player.Key, GameOutcome.Draw, 2);
                }
            }

            return results;
        }

        private int HasWinner(int[,] board)
        {
            // Check rows
            for (int i = 0; i < 3; i++)
            {
                if (board[i, 0] != 0 && board[i, 0] == board[i, 1] && board[i, 1] == board[i, 2])
                {
                    return board[i, 0];
                }
            }

            // Check columns
            for (int j = 0; j < 3; j++)
            {
                if (board[0, j] != 0 && board[0, j] == board[1, j] && board[1, j] == board[2, j])
                {
                    return board[0, j];
                }
            }

            // Check diagonals
            if (board[0, 0] != 0 && board[0, 0] == board[1, 1] && board[1, 1] == board[2, 2])
            {
                return board[0, 0];
            }

            if (board[0, 2] != 0 && board[0, 2] == board[1, 1] && board[1, 1] == board[2, 0])
            {
                return board[0, 2];
            }

            return 0; // No winner
        }
    }

    public class TicTacToeGameData
    {
        public int[,] Board { get; set; }
        public Dictionary<string, int> PlayerSymbols { get; set; }
        public string CurrentTurn { get; set; }
    }

    public class TicTacToeMoveData
    {
        public int Row { get; set; }
        public int Col { get; set; }
    }
}