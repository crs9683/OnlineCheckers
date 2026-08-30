namespace Checkers.Shared
{
    public class MatchStartedMessage
    {
        public string GameId { get; set; } = "";
        public int YourPlayer { get; set; }
        public string RedPlayerName { get; set; } = "";
        public string BlackPlayerName { get; set; } = "";
        public GameStateMessage GameState { get; set; } = new();
    }

    public class GameStateMessage
    {
        public int[][] Board { get; set; } = new int[0][];
        public int CurrentPlayer { get; set; }
        public bool MustContinueJump { get; set; }
        public int ContinuedJumpRow { get; set; } = -1;
        public int ContinuedJumpColumn { get; set; } = -1;
        public bool GameOver { get; set; }
        public int? Winner { get; set; }
    }

    public class MoveRequest
    {
        public string GameId { get; set; } = "";
        public int FromRow { get; set; }
        public int FromColumn { get; set; }
        public int ToRow { get; set; }
        public int ToColumn { get; set; }
    }
}