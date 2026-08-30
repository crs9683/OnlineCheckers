using System.Threading.Tasks;
using Checkers.Shared;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Checkers.Client
{
    public partial class MainWindow : Window
    {
        private readonly HubConnection connection =
            new HubConnectionBuilder()
                .WithUrl("https://localhost:7275/gamehub")
                .WithAutomaticReconnect()
                .Build();

        private GameStateMessage? gameState;
        private string gameId = "";
        private int myPlayer;
        private int selectedRow = -1;
        private int selectedColumn = -1;
        private bool gameOverMessageShown;
        private bool isSearching;

        public MainWindow()
        {
            InitializeComponent();

            RegisterServerMessages();
            RegisterConnectionEvents();
            DrawBoard();

            Loaded += MainWindow_Loaded;
        }

        private void RegisterConnectionEvents()
        {
            connection.Reconnecting += error =>
            {
                Dispatcher.Invoke(() =>
                {
                    ConnectionStatus.Text =
                        "Connection lost. Reconnecting...";

                    ConnectionStatus.Foreground =
                        Brushes.Gold;

                    FindGameButton.IsEnabled = false;
                    ResignButton.IsEnabled = false;
                    NewGameButton.IsEnabled = false;
                });

                return Task.CompletedTask;
            };

            connection.Reconnected += connectionId =>
            {
                Dispatcher.Invoke(() =>
                {
                    ConnectionStatus.Text =
                        "Reconnected to checkers server.";

                    ConnectionStatus.Foreground =
                        Brushes.LightGreen;

                    ResetForAnotherGame(
                        "Connection restored. Find a new game.");
                });

                return Task.CompletedTask;
            };

            connection.Closed += error =>
            {
                Dispatcher.Invoke(() =>
                {
                    ConnectionStatus.Text =
                        "Disconnected from server.";

                    ConnectionStatus.Foreground =
                        Brushes.OrangeRed;

                    FindGameButton.IsEnabled = false;
                    ResignButton.IsEnabled = false;
                    NewGameButton.IsEnabled = false;

                    GameStatus.Text =
                        "The server connection was closed.";
                });

                return Task.CompletedTask;
            };
        }

        private void RegisterServerMessages()
        {
            connection.On<string>(
                "WaitingForOpponent",
                message =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        isSearching = true;

                        UsernameTextBox.IsEnabled = false;
                        FindGameButton.Content = "Cancel Search";
                        FindGameButton.IsEnabled = true;

                        GameStatus.Text = message;
                    });
                });

            connection.On<string>(
                "QueueCancelled",
                message =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        isSearching = false;

                        UsernameTextBox.IsEnabled = true;
                        FindGameButton.Content = "Find Game";
                        FindGameButton.IsEnabled = true;

                        GameStatus.Text = message;
                    });
                });

            connection.On<MatchStartedMessage>(
                "MatchStarted",
                message =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        gameId = message.GameId;
                        myPlayer = message.YourPlayer;
                        gameState = message.GameState;
                        gameOverMessageShown = false;
                        isSearching = false;
                        FindGameButton.Content = "Find Game";
                        FindGameButton.IsEnabled = false;
                        ResignButton.IsEnabled = true;
                        NewGameButton.IsEnabled = false;

                        selectedRow = -1;
                        selectedColumn = -1;

                        string color =
                            myPlayer == 2 ? "Red" : "Black";

                        GameStatus.Text =
                            $"You are {color} — " +
                            $"{message.RedPlayerName} vs. " +
                            $"{message.BlackPlayerName}";

                        DrawBoard();
                    });
                });

            connection.On<GameStateMessage>(
                "GameUpdated",
                updatedState =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        gameState = updatedState;
                        ResignButton.IsEnabled =
                            !gameState.GameOver;
                        NewGameButton.IsEnabled =
                            gameState.GameOver;

                        if (gameState.MustContinueJump &&
                            gameState.CurrentPlayer == myPlayer)
                        {
                            selectedRow =
                                gameState.ContinuedJumpRow;

                            selectedColumn =
                                gameState.ContinuedJumpColumn;
                        }
                        else
                        {
                            selectedRow = -1;
                            selectedColumn = -1;
                        }

                        DrawBoard();
                        UpdateGameStatus();
                        ShowVictoryIfNeeded();
                    });
                });

            connection.On<string>(
                "MoveRejected",
                reason =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        GameStatus.Text =
                            $"Move rejected: {reason}";

                        selectedRow = -1;
                        selectedColumn = -1;
                        DrawBoard();
                    });
                });

            connection.On<string>(
                "OpponentDisconnected",
                message =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            message,
                            "Opponent Disconnected",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        ResetForAnotherGame(
                            "Your opponent disconnected. Ready for another game.");
                    });
                });
            connection.On<string>(
        "GameClosed",
        message =>
        {
            Dispatcher.Invoke(() =>
            {
                ResetForAnotherGame(message);
            });
        });
        }

        private async void MainWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                ConnectionStatus.Text =
                    "Connecting to server...";

                ConnectionStatus.Foreground =
                    Brushes.Gold;

                await connection.StartAsync();

                string response =
                    await connection.InvokeAsync<string>("Ping");

                ConnectionStatus.Text = response;
                ConnectionStatus.Foreground =
                    Brushes.LightGreen;

                FindGameButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ConnectionStatus.Text =
                    $"Connection failed: {ex.Message}";

                ConnectionStatus.Foreground =
                    Brushes.OrangeRed;
            }
        }

        private async void FindGameButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (isSearching)
            {
                try
                {
                    FindGameButton.IsEnabled = false;
                    GameStatus.Text = "Cancelling search...";

                    await connection.InvokeAsync("CancelQueue");
                }
                catch (Exception ex)
                {
                    FindGameButton.IsEnabled = true;

                    GameStatus.Text =
                        $"Could not cancel search: {ex.Message}";
                }

                return;
            }
            string username = UsernameTextBox.Text.Trim();

            if (username.Length == 0)
            {
                MessageBox.Show(
                    "Enter a username first.",
                    "Username Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            try
            {
                UsernameTextBox.IsEnabled = false;
                FindGameButton.IsEnabled = false;

                GameStatus.Text =
                    "Joining the matchmaking queue...";

                await connection.InvokeAsync(
                    "JoinQueue",
                    username);
            }
            catch (Exception ex)
            {
                UsernameTextBox.IsEnabled = true;
                FindGameButton.IsEnabled = true;

                GameStatus.Text =
                    $"Could not join: {ex.Message}";
            }
        }

        private async void ResignButton_Click(
    object sender,
    RoutedEventArgs e)
        {
            if (gameState == null ||
                gameState.GameOver ||
                gameId.Length == 0)
            {
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to resign?",
                "Resign Game",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                ResignButton.IsEnabled = false;

                await connection.InvokeAsync(
                    "ResignGame",
                    gameId);
            }
            catch (Exception ex)
            {
                ResignButton.IsEnabled = true;

                GameStatus.Text =
                    $"Could not resign: {ex.Message}";
            }
        }

        private async void NewGameButton_Click(
    object sender,
    RoutedEventArgs e)
        {
            if (gameState == null ||
                !gameState.GameOver ||
                gameId.Length == 0)
            {
                return;
            }

            try
            {
                NewGameButton.IsEnabled = false;

                await connection.InvokeAsync(
                    "LeaveFinishedGame",
                    gameId);
            }
            catch (Exception ex)
            {
                NewGameButton.IsEnabled = true;

                GameStatus.Text =
                    $"Could not leave game: {ex.Message}";
            }
        }

        private async void Square_Click(
            object sender,
            MouseButtonEventArgs e)
        {
            if (gameState == null ||
                gameState.GameOver ||
                gameState.CurrentPlayer != myPlayer)
            {
                return;
            }

            Grid clickedSquare = (Grid)sender;
            (int row, int column) =
                ((int, int))clickedSquare.Tag;

            int clickedPiece =
                gameState.Board[row][column];

            if (GetOwner(clickedPiece) == myPlayer)
            {
                if (gameState.MustContinueJump &&
                    (row != gameState.ContinuedJumpRow ||
                     column != gameState.ContinuedJumpColumn))
                {
                    return;
                }

                selectedRow = row;
                selectedColumn = column;

                DrawBoard();
                return;
            }

            if (selectedRow == -1 || clickedPiece != 0)
                return;

            MoveRequest request = new()
            {
                GameId = gameId,
                FromRow = selectedRow,
                FromColumn = selectedColumn,
                ToRow = row,
                ToColumn = column
            };

            selectedRow = -1;
            selectedColumn = -1;
            DrawBoard();

            try
            {
                await connection.InvokeAsync(
                    "MakeMove",
                    request);
            }
            catch (Exception ex)
            {
                GameStatus.Text =
                    $"Move failed: {ex.Message}";
            }
        }

        private void ResetForAnotherGame(string message)
        {
            gameState = null;
            gameId = "";
            myPlayer = 0;

            selectedRow = -1;
            selectedColumn = -1;
            gameOverMessageShown = false;

            UsernameTextBox.IsEnabled = true;
            isSearching = false;
            FindGameButton.Content = "Find Game";
            FindGameButton.IsEnabled =
                connection.State ==
                Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected;

            ResignButton.IsEnabled = false;
            NewGameButton.IsEnabled = false;

            GameStatus.Text = message;

            DrawBoard();
        }

        private void DrawBoard()
        {
            BoardGrid.Children.Clear();

            bool flipBoard =
                gameState != null && myPlayer == 1;

            for (int displayRow = 0; displayRow < 8; displayRow++)
            {
                for (int displayColumn = 0;
                     displayColumn < 8;
                     displayColumn++)
                {
                    int boardRow = flipBoard
                        ? 7 - displayRow
                        : displayRow;

                    int boardColumn = flipBoard
                        ? 7 - displayColumn
                        : displayColumn;

                    Grid square = new Grid
                    {
                        // Store the actual board coordinates,
                        // even when the display is rotated.
                        Tag = (boardRow, boardColumn)
                    };

                    square.MouseLeftButtonDown += Square_Click;

                    bool isDarkSquare =
                        (boardRow + boardColumn) % 2 != 0;

                    bool isSelected =
                        boardRow == selectedRow &&
                        boardColumn == selectedColumn;

                    if (isSelected)
                    {
                        square.Background = Brushes.Gold;
                    }
                    else
                    {
                        square.Background =
                            new SolidColorBrush(
                                isDarkSquare
                                    ? Color.FromRgb(110, 72, 45)
                                    : Color.FromRgb(238, 220, 190));
                    }

                    if (gameState != null)
                    {
                        int piece =
                            gameState.Board[boardRow][boardColumn];

                        if (piece == 1)
                            AddPiece(square, Colors.Black, false);
                        else if (piece == 2)
                            AddPiece(square, Colors.Red, false);
                        else if (piece == 3)
                            AddPiece(square, Colors.Black, true);
                        else if (piece == 4)
                            AddPiece(square, Colors.Red, true);
                    }

                    BoardGrid.Children.Add(square);
                }
            }
        }

        private void UpdateGameStatus()
        {
            if (gameState == null)
                return;

            if (gameState.GameOver)
            {
                string winner =
                    gameState.Winner == 2 ? "Red" : "Black";

                GameStatus.Text = $"{winner} wins!";
                return;
            }

            string player =
                gameState.CurrentPlayer == 2
                    ? "Red"
                    : "Black";

            if (gameState.MustContinueJump)
                GameStatus.Text =
                    $"{player} must continue jumping.";
            else
                GameStatus.Text =
                    $"{player}'s turn.";
        }

        private void ShowVictoryIfNeeded()
        {
            if (gameState == null ||
                !gameState.GameOver ||
                gameOverMessageShown)
            {
                return;
            }

            gameOverMessageShown = true;

            string winner =
                gameState.Winner == 2 ? "Red" : "Black";

            MessageBox.Show(
                $"{winner} wins!",
                "Game Over",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private static int GetOwner(int piece)
        {
            if (piece == 1 || piece == 3)
                return 1;

            if (piece == 2 || piece == 4)
                return 2;

            return 0;
        }

        private static void AddPiece(
            Grid square,
            Color color,
            bool isKing)
        {
            Ellipse piece = new Ellipse
            {
                Fill = new SolidColorBrush(color),
                Stroke = Brushes.White,
                StrokeThickness = 2,
                Margin = new Thickness(8),
                IsHitTestVisible = false
            };

            square.Children.Add(piece);

            if (isKing)
            {
                TextBlock crown = new TextBlock
                {
                    Text = "♛",
                    Foreground = Brushes.Gold,
                    FontSize = 35,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment =
                        HorizontalAlignment.Center,
                    VerticalAlignment =
                        VerticalAlignment.Center,
                    IsHitTestVisible = false
                };

                square.Children.Add(crown);
            }
        }
    }
}