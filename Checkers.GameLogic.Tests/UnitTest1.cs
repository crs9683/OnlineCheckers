using Checkers.GameLogic;
using Xunit;

namespace Checkers.GameLogic.Tests
{
    public class CheckersGameTests
    {
        [Fact]
        public void NewGameStartsWithTwelvePiecesPerPlayer()
        {
            CheckersGame game = new();

            int redPieces = 0;
            int blackPieces = 0;

            for (int row = 0; row < 8; row++)
            {
                for (int column = 0; column < 8; column++)
                {
                    Player? owner =
                        CheckersGame.GetOwner(
                            game.Board[row, column]);

                    if (owner == Player.Red)
                        redPieces++;

                    if (owner == Player.Black)
                        blackPieces++;
                }
            }

            Assert.Equal(12, redPieces);
            Assert.Equal(12, blackPieces);
        }

        [Fact]
        public void RedMovesFirst()
        {
            CheckersGame game = new();

            Assert.Equal(Player.Red, game.CurrentPlayer);
        }

        [Fact]
        public void LegalMoveChangesBoardAndTurn()
        {
            CheckersGame game = new();

            bool succeeded = game.TryMove(
                5, 0,
                4, 1);

            Assert.True(succeeded);
            Assert.Equal(Piece.Empty, game.Board[5, 0]);
            Assert.Equal(Piece.Red, game.Board[4, 1]);
            Assert.Equal(Player.Black, game.CurrentPlayer);
        }

        [Fact]
        public void StraightMoveIsRejected()
        {
            CheckersGame game = new();

            bool succeeded = game.TryMove(
                5, 0,
                4, 0);

            Assert.False(succeeded);
            Assert.Equal(Piece.Red, game.Board[5, 0]);
            Assert.Equal(Player.Red, game.CurrentPlayer);
        }

        [Fact]
        public void CaptureRemovesOpponentPiece()
        {
            CheckersGame game = new();

            Array.Clear(
                game.Board,
                0,
                game.Board.Length);

            game.Board[5, 0] = Piece.Red;
            game.Board[4, 1] = Piece.Black;
            game.Board[0, 1] = Piece.Black;

            bool succeeded = game.TryMove(
                5, 0,
                3, 2);

            Assert.True(succeeded);
            Assert.Equal(Piece.Empty, game.Board[5, 0]);
            Assert.Equal(Piece.Empty, game.Board[4, 1]);
            Assert.Equal(Piece.Red, game.Board[3, 2]);
        }

        [Fact]
        public void CaptureIsRequiredWhenAvailable()
        {
            CheckersGame game = new();

            Array.Clear(
                game.Board,
                0,
                game.Board.Length);

            game.Board[5, 0] = Piece.Red;
            game.Board[4, 1] = Piece.Black;
            game.Board[5, 4] = Piece.Red;
            game.Board[0, 1] = Piece.Black;

            bool succeeded = game.TryMove(
                5, 4,
                4, 3);

            Assert.False(succeeded);
            Assert.Equal(Piece.Red, game.Board[5, 4]);
            Assert.Equal(Player.Red, game.CurrentPlayer);
        }

        [Fact]
        public void RedPieceBecomesKingOnFinalRow()
        {
            CheckersGame game = new();

            Array.Clear(
                game.Board,
                0,
                game.Board.Length);

            game.Board[1, 2] = Piece.Red;
            game.Board[0, 1] = Piece.Black;

            bool succeeded = game.TryMove(
                1, 2,
                0, 3);

            Assert.True(succeeded);
            Assert.Equal(
                Piece.RedKing,
                game.Board[0, 3]);
        }

        [Fact]
        public void PlayerWinsWhenOpponentHasNoLegalMoves()
        {
            CheckersGame game = new();

            Array.Clear(
                game.Board,
                0,
                game.Board.Length);

            game.Board[5, 0] = Piece.Red;

            bool succeeded = game.TryMove(
                5, 0,
                4, 1);

            Assert.True(succeeded);
            Assert.True(game.GameOver);
            Assert.Equal(Player.Red, game.Winner);
        }
    }
}