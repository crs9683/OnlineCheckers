using Checkers.GameLogic;
using Checkers.Shared;
using Microsoft.AspNetCore.SignalR;

namespace Checkers.Server
{
    public class GameHub : Hub
    {
        private static readonly object SyncRoot = new();

        private static WaitingPlayer? waitingPlayer;

        private static readonly Dictionary<string, GameRoom> games =
            new();

        public string Ping()
        {
            return "Checkers server is online!";
        }

        public async Task JoinQueue(string username)
        {
            username = username?.Trim() ?? "";

            if (username.Length == 0)
                username = "Guest";

            if (username.Length > 20)
                username = username[..20];

            WaitingPlayer? opponent = null;
            GameRoom? newGame = null;

            lock (SyncRoot)
            {
                if (waitingPlayer == null)
                {
                    waitingPlayer = new WaitingPlayer
                    {
                        ConnectionId = Context.ConnectionId,
                        Username = username
                    };
                }
                else if (waitingPlayer.ConnectionId ==
                         Context.ConnectionId)
                {
                    // This player is already waiting.
                }
                else
                {
                    opponent = waitingPlayer;
                    waitingPlayer = null;

                    newGame = new GameRoom
                    {
                        GameId = Guid.NewGuid().ToString(),
                        RedConnectionId = opponent.ConnectionId,
                        RedPlayerName = opponent.Username,
                        BlackConnectionId = Context.ConnectionId,
                        BlackPlayerName = username
                    };

                    games[newGame.GameId] = newGame;
                }
            }

            if (newGame == null || opponent == null)
            {
                await Clients.Caller.SendAsync(
                    "WaitingForOpponent",
                    "Waiting for another player...");

                return;
            }

            await Groups.AddToGroupAsync(
                newGame.RedConnectionId,
                newGame.GameId);

            await Groups.AddToGroupAsync(
                newGame.BlackConnectionId,
                newGame.GameId);

            GameStateMessage gameState =
                CreateGameState(newGame.Game);

            MatchStartedMessage redMessage = new()
            {
                GameId = newGame.GameId,
                YourPlayer = (int)Player.Red,
                RedPlayerName = newGame.RedPlayerName,
                BlackPlayerName = newGame.BlackPlayerName,
                GameState = gameState
            };

            MatchStartedMessage blackMessage = new()
            {
                GameId = newGame.GameId,
                YourPlayer = (int)Player.Black,
                RedPlayerName = newGame.RedPlayerName,
                BlackPlayerName = newGame.BlackPlayerName,
                GameState = gameState
            };

            await Clients.Client(newGame.RedConnectionId)
                .SendAsync("MatchStarted", redMessage);

            await Clients.Client(newGame.BlackConnectionId)
                .SendAsync("MatchStarted", blackMessage);
        }
        public async Task CancelQueue()
        {
            bool wasWaiting = false;

            lock (SyncRoot)
            {
                if (waitingPlayer?.ConnectionId ==
                    Context.ConnectionId)
                {
                    waitingPlayer = null;
                    wasWaiting = true;
                }
            }

            string message = wasWaiting
                ? "Matchmaking search cancelled."
                : "You were not waiting for an opponent.";

            await Clients.Caller.SendAsync(
                "QueueCancelled",
                message);
        }

        public async Task MakeMove(MoveRequest request)
        {
            GameRoom? room;

            // Use the global lock only for the quick dictionary lookup.
            lock (SyncRoot)
            {
                games.TryGetValue(request.GameId, out room);
            }

            if (room == null)
            {
                await Clients.Caller.SendAsync(
                    "MoveRejected",
                    "That game no longer exists.");

                return;
            }

            string? rejectionReason = null;
            GameStateMessage? updatedState = null;

            // Only this individual game is locked while validating a move.
            lock (room.GameLock)
            {
                Player? callerPlayer =
                    GetPlayerForConnection(
                        room,
                        Context.ConnectionId);

                if (callerPlayer == null)
                {
                    rejectionReason =
                        "You are not a player in this game.";
                }
                else if (callerPlayer != room.Game.CurrentPlayer)
                {
                    rejectionReason =
                        "It is not your turn.";
                }
                else
                {
                    bool moveSucceeded = room.Game.TryMove(
                        request.FromRow,
                        request.FromColumn,
                        request.ToRow,
                        request.ToColumn);

                    if (!moveSucceeded)
                    {
                        rejectionReason =
                            "That move is not legal.";
                    }
                    else
                    {
                        updatedState =
                            CreateGameState(room.Game);
                    }
                }
            }

            if (rejectionReason != null)
            {
                await Clients.Caller.SendAsync(
                    "MoveRejected",
                    rejectionReason);

                return;
            }

            if (updatedState != null)
            {
                await Clients.Group(room.GameId)
                    .SendAsync("GameUpdated", updatedState);
            }
        }

        public async Task ResignGame(string gameId)
        {
            GameRoom? room;

            lock (SyncRoot)
            {
                games.TryGetValue(gameId, out room);
            }

            if (room == null)
            {
                await Clients.Caller.SendAsync(
                    "MoveRejected",
                    "That game no longer exists.");

                return;
            }

            string? errorMessage = null;
            GameStateMessage? updatedState = null;

            lock (room.GameLock)
            {
                Player? resigningPlayer =
                    GetPlayerForConnection(
                        room,
                        Context.ConnectionId);

                if (resigningPlayer == null)
                {
                    errorMessage =
                        "You are not a player in this game.";
                }
                else if (!room.Game.Resign(resigningPlayer.Value))
                {
                    errorMessage =
                        "The game has already ended.";
                }
                else
                {
                    updatedState =
                        CreateGameState(room.Game);
                }
            }

            if (errorMessage != null)
            {
                await Clients.Caller.SendAsync(
                    "MoveRejected",
                    errorMessage);

                return;
            }

            if (updatedState != null)
            {
                await Clients.Group(room.GameId)
                    .SendAsync("GameUpdated", updatedState);
            }
        }

        public async Task LeaveFinishedGame(string gameId)
        {
            GameRoom? room;

            lock (SyncRoot)
            {
                games.TryGetValue(gameId, out room);
            }

            if (room == null)
            {
                await Clients.Caller.SendAsync(
                    "MoveRejected",
                    "That game no longer exists.");

                return;
            }

            string? errorMessage = null;

            lock (room.GameLock)
            {
                if (GetPlayerForConnection(
                        room,
                        Context.ConnectionId) == null)
                {
                    errorMessage =
                        "You are not a player in this game.";
                }
                else if (!room.Game.GameOver)
                {
                    errorMessage =
                        "The game has not ended yet.";
                }
                else
                {
                    lock (SyncRoot)
                    {
                        games.Remove(gameId);
                    }
                }
            }

            if (errorMessage != null)
            {
                await Clients.Caller.SendAsync(
                    "MoveRejected",
                    errorMessage);

                return;
            }

            await Clients.Group(room.GameId)
                .SendAsync(
                    "GameClosed",
                    "Ready to find another game.");

            await Groups.RemoveFromGroupAsync(
                room.RedConnectionId,
                room.GameId);

            await Groups.RemoveFromGroupAsync(
                room.BlackConnectionId,
                room.GameId);
        }

        public override async Task OnDisconnectedAsync(
    Exception? exception)
        {
            string? opponentConnectionId = null;
            GameRoom? disconnectedGame = null;
            bool gameWasRemoved = false;

            // Find the affected game, but do not hold the global
            // lock while waiting for that game's individual lock.
            lock (SyncRoot)
            {
                if (waitingPlayer?.ConnectionId ==
                    Context.ConnectionId)
                {
                    waitingPlayer = null;
                }

                foreach (GameRoom room in games.Values)
                {
                    if (room.RedConnectionId ==
                        Context.ConnectionId)
                    {
                        disconnectedGame = room;
                        opponentConnectionId =
                            room.BlackConnectionId;
                        break;
                    }

                    if (room.BlackConnectionId ==
                        Context.ConnectionId)
                    {
                        disconnectedGame = room;
                        opponentConnectionId =
                            room.RedConnectionId;
                        break;
                    }
                }
            }

            if (disconnectedGame != null)
            {
                lock (disconnectedGame.GameLock)
                {
                    lock (SyncRoot)
                    {
                        if (games.TryGetValue(
                                disconnectedGame.GameId,
                                out GameRoom? existingRoom) &&
                            ReferenceEquals(
                                existingRoom,
                                disconnectedGame))
                        {
                            games.Remove(
                                disconnectedGame.GameId);

                            gameWasRemoved = true;
                        }
                    }
                }
            }

            if (gameWasRemoved &&
                opponentConnectionId != null)
            {
                await Clients.Client(opponentConnectionId)
                    .SendAsync(
                        "OpponentDisconnected",
                        "Your opponent disconnected.");
            }

            await base.OnDisconnectedAsync(exception);
        }

        private static Player? GetPlayerForConnection(
            GameRoom room,
            string connectionId)
        {
            if (room.RedConnectionId == connectionId)
                return Player.Red;

            if (room.BlackConnectionId == connectionId)
                return Player.Black;

            return null;
        }

        private static GameStateMessage CreateGameState(
            CheckersGame game)
        {
            int[][] board = new int[8][];

            for (int row = 0; row < 8; row++)
            {
                board[row] = new int[8];

                for (int column = 0; column < 8; column++)
                {
                    board[row][column] =
                        (int)game.Board[row, column];
                }
            }

            return new GameStateMessage
            {
                Board = board,
                CurrentPlayer = (int)game.CurrentPlayer,
                MustContinueJump = game.MustContinueJump,
                ContinuedJumpRow = game.ContinuedJumpRow,
                ContinuedJumpColumn = game.ContinuedJumpColumn,
                GameOver = game.GameOver,
                Winner = game.Winner == null
                    ? null
                    : (int)game.Winner.Value
            };
        }

        private sealed class WaitingPlayer
        {
            public string ConnectionId { get; set; } = "";
            public string Username { get; set; } = "";
        }

        private sealed class GameRoom
        {
            public object GameLock { get; } = new();
            public string GameId { get; set; } = "";
            public string RedConnectionId { get; set; } = "";
            public string BlackConnectionId { get; set; } = "";
            public string RedPlayerName { get; set; } = "";
            public string BlackPlayerName { get; set; } = "";
            public CheckersGame Game { get; } = new();
        }
    }
}