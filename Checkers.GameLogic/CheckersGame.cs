using System;

namespace Checkers.GameLogic
{
    public enum Piece
    {
        Empty = 0,
        Black = 1,
        Red = 2,
        BlackKing = 3,
        RedKing = 4
    }

    public enum Player
    {
        Black = 1,
        Red = 2
    }

    public class CheckersGame
    {
        public Piece[,] Board { get; } = new Piece[8, 8];

        public Player CurrentPlayer { get; private set; }
        public bool MustContinueJump { get; private set; }
        public int ContinuedJumpRow { get; private set; } = -1;
        public int ContinuedJumpColumn { get; private set; } = -1;
        public bool GameOver { get; private set; }
        public Player? Winner { get; private set; }

        public CheckersGame()
        {
            Reset();
        }

        public void Reset()
        {
            Array.Clear(Board, 0, Board.Length);

            CurrentPlayer = Player.Red;
            MustContinueJump = false;
            ContinuedJumpRow = -1;
            ContinuedJumpColumn = -1;
            GameOver = false;
            Winner = null;

            for (int row = 0; row < 8; row++)
            {
                for (int column = 0; column < 8; column++)
                {
                    if ((row + column) % 2 == 0)
                        continue;

                    if (row <= 2)
                        Board[row, column] = Piece.Black;
                    else if (row >= 5)
                        Board[row, column] = Piece.Red;
                }
            }
        }

        public bool CanSelectPiece(int row, int column)
        {
            if (GameOver || !IsInsideBoard(row, column))
                return false;

            if (GetOwner(Board[row, column]) != CurrentPlayer)
                return false;

            if (MustContinueJump)
            {
                return row == ContinuedJumpRow &&
                       column == ContinuedJumpColumn;
            }

            if (PlayerHasCapture(CurrentPlayer))
                return PieceHasCapture(row, column);

            return true;
        }

        public bool TryMove(
            int fromRow,
            int fromColumn,
            int toRow,
            int toColumn)
        {
            if (GameOver ||
                !IsInsideBoard(fromRow, fromColumn) ||
                !IsInsideBoard(toRow, toColumn))
            {
                return false;
            }

            if (!CanSelectPiece(fromRow, fromColumn))
                return false;

            if (Board[toRow, toColumn] != Piece.Empty)
                return false;

            Piece movingPiece = Board[fromRow, fromColumn];

            int rowDifference = toRow - fromRow;
            int columnDifference = toColumn - fromColumn;
            int requiredDirection =
                CurrentPlayer == Player.Red ? -1 : 1;

            bool isKing = IsKing(movingPiece);

            bool isNormalMove =
                Math.Abs(rowDifference) == 1 &&
                Math.Abs(columnDifference) == 1 &&
                (isKing || rowDifference == requiredDirection);

            if (isNormalMove)
            {
                if (MustContinueJump ||
                    PlayerHasCapture(CurrentPlayer))
                {
                    return false;
                }

                Board[toRow, toColumn] = movingPiece;
                Board[fromRow, fromColumn] = Piece.Empty;

                PromotePiece(toRow, toColumn);
                FinishTurn();

                return true;
            }

            bool isJump =
                Math.Abs(rowDifference) == 2 &&
                Math.Abs(columnDifference) == 2 &&
                (isKing || rowDifference == requiredDirection * 2);

            if (!isJump)
                return false;

            int jumpedRow = (fromRow + toRow) / 2;
            int jumpedColumn = (fromColumn + toColumn) / 2;
            Piece jumpedPiece = Board[jumpedRow, jumpedColumn];

            Player? jumpedOwner = GetOwner(jumpedPiece);

            if (jumpedOwner == null ||
                jumpedOwner == CurrentPlayer)
            {
                return false;
            }

            Board[toRow, toColumn] = movingPiece;
            Board[fromRow, fromColumn] = Piece.Empty;
            Board[jumpedRow, jumpedColumn] = Piece.Empty;

            bool wasPromoted = PromotePiece(toRow, toColumn);

            if (!wasPromoted && PieceHasCapture(toRow, toColumn))
            {
                MustContinueJump = true;
                ContinuedJumpRow = toRow;
                ContinuedJumpColumn = toColumn;
            }
            else
            {
                FinishTurn();
            }

            return true;
        }
        public bool Resign(Player resigningPlayer)
        {
            if (GameOver)
                return false;

            Winner = resigningPlayer == Player.Red
                ? Player.Black
                : Player.Red;

            GameOver = true;
            MustContinueJump = false;
            ContinuedJumpRow = -1;
            ContinuedJumpColumn = -1;

            return true;
        }
        public bool PlayerHasCapture(Player player)
        {
            for (int row = 0; row < 8; row++)
            {
                for (int column = 0; column < 8; column++)
                {
                    if (GetOwner(Board[row, column]) == player &&
                        PieceHasCapture(row, column))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool PieceHasCapture(int row, int column)
        {
            if (!IsInsideBoard(row, column))
                return false;

            Piece piece = Board[row, column];

            if (piece == Piece.Empty)
                return false;

            if (IsKing(piece))
            {
                return CanCapture(row, column, -1, -1) ||
                       CanCapture(row, column, -1, 1) ||
                       CanCapture(row, column, 1, -1) ||
                       CanCapture(row, column, 1, 1);
            }

            int direction =
                GetOwner(piece) == Player.Red ? -1 : 1;

            return CanCapture(row, column, direction, -1) ||
                   CanCapture(row, column, direction, 1);
        }

        public static Player? GetOwner(Piece piece)
        {
            if (piece == Piece.Black ||
                piece == Piece.BlackKing)
            {
                return Player.Black;
            }

            if (piece == Piece.Red ||
                piece == Piece.RedKing)
            {
                return Player.Red;
            }

            return null;
        }

        public static bool IsKing(Piece piece)
        {
            return piece == Piece.BlackKing ||
                   piece == Piece.RedKing;
        }

        private bool CanCapture(
            int row,
            int column,
            int rowDirection,
            int columnDirection)
        {
            int middleRow = row + rowDirection;
            int middleColumn = column + columnDirection;
            int landingRow = row + rowDirection * 2;
            int landingColumn = column + columnDirection * 2;

            if (!IsInsideBoard(middleRow, middleColumn) ||
                !IsInsideBoard(landingRow, landingColumn))
            {
                return false;
            }

            Player? player = GetOwner(Board[row, column]);
            Player? middlePlayer =
                GetOwner(Board[middleRow, middleColumn]);

            return middlePlayer != null &&
                   middlePlayer != player &&
                   Board[landingRow, landingColumn] == Piece.Empty;
        }

        private bool PromotePiece(int row, int column)
        {
            if (Board[row, column] == Piece.Red && row == 0)
            {
                Board[row, column] = Piece.RedKing;
                return true;
            }

            if (Board[row, column] == Piece.Black && row == 7)
            {
                Board[row, column] = Piece.BlackKing;
                return true;
            }

            return false;
        }

        private void FinishTurn()
        {
            MustContinueJump = false;
            ContinuedJumpRow = -1;
            ContinuedJumpColumn = -1;

            CurrentPlayer = CurrentPlayer == Player.Red
                ? Player.Black
                : Player.Red;

            if (!PlayerHasAnyLegalMove(CurrentPlayer))
            {
                GameOver = true;

                Winner = CurrentPlayer == Player.Red
                    ? Player.Black
                    : Player.Red;
            }
        }

        private bool PlayerHasAnyLegalMove(Player player)
        {
            for (int row = 0; row < 8; row++)
            {
                for (int column = 0; column < 8; column++)
                {
                    Piece piece = Board[row, column];

                    if (GetOwner(piece) != player)
                        continue;

                    if (PieceHasCapture(row, column) ||
                        PieceHasNormalMove(row, column))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool PieceHasNormalMove(int row, int column)
        {
            Piece piece = Board[row, column];

            if (piece == Piece.Empty)
                return false;

            if (IsKing(piece))
            {
                return CanMoveTo(row - 1, column - 1) ||
                       CanMoveTo(row - 1, column + 1) ||
                       CanMoveTo(row + 1, column - 1) ||
                       CanMoveTo(row + 1, column + 1);
            }

            int direction =
                GetOwner(piece) == Player.Red ? -1 : 1;

            return CanMoveTo(row + direction, column - 1) ||
                   CanMoveTo(row + direction, column + 1);
        }

        private bool CanMoveTo(int row, int column)
        {
            return IsInsideBoard(row, column) &&
                   Board[row, column] == Piece.Empty;
        }

        private static bool IsInsideBoard(int row, int column)
        {
            return row >= 0 && row < 8 &&
                   column >= 0 && column < 8;
        }
    }
}