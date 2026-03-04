using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using System.Formats.Tar;

namespace Simple2048
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new GameForm());
        }
    }

    public class GameForm : Form
    {
        private bool IsFirstLaunch()
        {
            string path = "tutorial.flag";

            if (File.Exists(path))
        return false;

    File.WriteAllText(path, "shown");
    return true;
}
        private void ShowTutorial()
{
    MessageBox.Show(
        "欢迎来到 2048！\n\n" +
        "使用方向键移动方块。\n" +
        "相同数字会合并。\n" +
        "目标是得到 2048。\n\n" +
        "提示：按 Ctrl+Z 可以撤销一步。",
        "游戏教程",
        MessageBoxButtons.OK,
        MessageBoxIcon.Information
    );
}
        private void EnterAiMode()
        {
            currentState = GameState.AiTurn;
        }

        private void ExitAiMode()
        {
            currentState = GameState.PlayerTurn;
        }
        private enum GameState
        {
            PlayerTurn,
            AiTurn,
            GameOver
        }

        private GameState currentState = GameState.PlayerTurn;
        private void SelfCheck()
        {
            string exe = Application.ExecutablePath;

    if (!File.Exists(exe))
    {
        Environment.FailFast("Integrity check failed.");
    }
        }
        private Label[,] cells = new Label[4, 4];
        private int[,] board = new int[4, 4];
        private Random rand = new Random();
        private Stack<int[,]> boardHistory = new();
        private Stack<int> scoreHistory = new();
        private void SaveState()
        {
            boardHistory.Push((int[,])board.Clone());
            scoreHistory.Push(score);
        }

        private int score = 0;
        private int bestScore = 0;
        private bool isAiRunning = false;
        private bool isAnimating = false;
        private class MoveInfo
        {
            public Label Tile;
            public Point From;
            public Point To;
        }
        private readonly string bestPath =
            Path.Combine(Application.StartupPath, "best.json");
        private void Undo()
        {
        if (boardHistory.Count == 0)
            return;

            board = boardHistory.Pop();
            score = scoreHistory.Pop();

            UpdateUI();
        }

        private string GetSecretKey()
{
    string part1 = "2048";
    string part2 = Environment.MachineName;
    string part3 = Environment.UserName;

    string raw = part1 + part2 + part3;

    using var sha = System.Security.Cryptography.SHA256.Create();
    var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
    return Convert.ToBase64String(bytes);
}

        private System.Windows.Forms.Timer? aiTimer;

        public GameForm()
        {
            #if !DEBUG
        SelfCheck();
            #endif
            Shown += (_, _) =>
            {
            if (IsFirstLaunch())
                ShowTutorial();
            };
            Text = "2048";
            ClientSize = new Size(420, 480);
            StartPosition = FormStartPosition.CenterScreen;

            LoadBestScore();
            InitGrid();
            InitMenu();
            ResetGame();

            KeyDown += GameForm_KeyDown;
            

}


        private void InitMenu()
        {
            var menu = new MenuStrip();

            var file = new ToolStripMenuItem("File");
            file.DropDownItems.Add("Save", null, SaveMenu_Click);
            file.DropDownItems.Add("Load", null, LoadMenu_Click);
            file.DropDownItems.Add("Restart", null, (_, _) => ResetGame());

            var ai = new ToolStripMenuItem("Smart AI");
            ai.Click += (_, _) => ToggleAI();

            menu.Items.Add(file);
            menu.Items.Add(ai);

            Controls.Add(menu);
            MainMenuStrip = menu;
        }
        private string ComputeHmac(string data)
{
    using var hmac = new System.Security.Cryptography.HMACSHA256(
        Encoding.UTF8.GetBytes(GetSecretKey()));

    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    return Convert.ToBase64String(hash);
}

        private void InitGrid()
        {
            int size = 90;
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                {
                    var lbl = new Label
                    {
                        BorderStyle = BorderStyle.FixedSingle,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Font = new Font("Arial", 22, FontStyle.Bold),
                        BackColor = Color.LightGray,
                        Location = new Point(c * (size + 5) + 20, r * (size + 5) + 40),
                        Size = new Size(size, size)
                    };
                    Controls.Add(lbl);
                    cells[r, c] = lbl;
                }
        }

        private void ResetGame()
        {
            board = new int[4, 4];
            score = 0;
            SpawnTile();
            SpawnTile();
            UpdateUI();
        }

        private void LoadBestScore()
        {
            if (File.Exists(bestPath))
                bestScore = JsonSerializer.Deserialize<int>(
                    File.ReadAllText(bestPath));
        }

        private void SaveBestScore()
        {
            File.WriteAllText(bestPath,
                JsonSerializer.Serialize(bestScore));
        }

        private void UpdateUI()
        {
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                {
                    int val = board[r, c];
                    cells[r, c].Text = val == 0 ? "" : val.ToString();
                    cells[r, c].BackColor = GetColor(val);
                }

            Text = $"2048   Score:{score}   Best:{bestScore}";
        }

        private Color GetColor(int val)
        {
            if (val == 0) return Color.LightGray;
            int power = (int)Math.Log2(val);
            return Color.FromArgb(
                (power * 40) % 255,
                (power * 80) % 255,
                (power * 120) % 255);
        }

        private void SpawnTile()
        {
            var empty = new System.Collections.Generic.List<(int, int)>();
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    if (board[r, c] == 0) empty.Add((r, c));

            if (empty.Count == 0) return;

            var (rr, cc) = empty[rand.Next(empty.Count)];
            board[rr, cc] = rand.Next(10) == 0 ? 4 : 2;
        }

        private int[] MergeLine(int[] oldLine)
        {
            var list = new System.Collections.Generic.List<int>();
            foreach (var v in oldLine)
                if (v != 0) list.Add(v);

            for (int i = 0; i < list.Count - 1; i++)
                if (list[i] == list[i + 1])
                {
                    list[i] *= 2;
                    score += list[i];
                    if (score > bestScore)
                    {
                        bestScore = score;
                        SaveBestScore();
                    }
                    list.RemoveAt(i + 1);
                }

            while (list.Count < 4) list.Add(0);
            return list.ToArray();
        }

        private bool DoMove(Func<bool> moveFunc)
        {
            SaveState();
            bool moved = moveFunc();
            if (moved)
            {
                SpawnTile();
                UpdateUI();
                if (IsGameOver())
                    MessageBox.Show("Game Over");
            }
            else
            {
                boardHistory.Pop();
                scoreHistory.Pop();
            }
            return moved;
        }

        private bool MoveLeft()
        {
            bool moved = false;
            for (int r = 0; r < 4; r++)
            {
                int[] row = new int[4];
                for (int c = 0; c < 4; c++) row[c] = board[r, c];
                var merged = MergeLine(row);
                for (int c = 0; c < 4; c++)
                {
                    if (board[r, c] != merged[c]) moved = true;
                    board[r, c] = merged[c];
                }
            }
            return moved;
        }

        private bool MoveRight()
        {
            bool moved = false;
            for (int r = 0; r < 4; r++)
            {
                int[] row = new int[4];
                for (int c = 0; c < 4; c++) row[3 - c] = board[r, c];
                var merged = MergeLine(row);
                for (int c = 0; c < 4; c++)
                {
                    if (board[r, c] != merged[3 - c]) moved = true;
                    board[r, c] = merged[3 - c];
                }
            }
            return moved;
        }

        private bool MoveUp()
        {
            bool moved = false;
            for (int c = 0; c < 4; c++)
            {
                int[] col = new int[4];
                for (int r = 0; r < 4; r++) col[r] = board[r, c];
                var merged = MergeLine(col);
                for (int r = 0; r < 4; r++)
                {
                    if (board[r, c] != merged[r]) moved = true;
                    board[r, c] = merged[r];
                }
            }
            return moved;
        }

        private bool MoveDown()
        {
            bool moved = false;
            for (int c = 0; c < 4; c++)
            {
                int[] col = new int[4];
                for (int r = 0; r < 4; r++) col[3 - r] = board[r, c];
                var merged = MergeLine(col);
                for (int r = 0; r < 4; r++)
                {
                    if (board[r, c] != merged[3 - r]) moved = true;
                    board[r, c] = merged[3 - r];
                }
            }
            return moved;
        }

        private bool IsGameOver()
        {
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    if (board[r, c] == 0) return false;

            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 3; c++)
                    if (board[r, c] == board[r, c + 1]) return false;

            for (int c = 0; c < 4; c++)
                for (int r = 0; r < 3; r++)
                    if (board[r, c] == board[r + 1, c]) return false;

            return true;
        }

        private void ToggleAI()
        {
            if (aiTimer != null)
            {
                aiTimer.Stop();
                aiTimer = null;
                ExitAiMode();
                return;
            }
            EnterAiMode();
            aiTimer = new System.Windows.Forms.Timer { Interval = 150 };
            aiTimer.Tick += (_, _) =>
            {
                var move = GetBestMove();
                switch (move)
                {
                    case Keys.Left: DoMove(MoveLeft); break;
                    case Keys.Right: DoMove(MoveRight); break;
                    case Keys.Up: DoMove(MoveUp); break;
                    case Keys.Down: DoMove(MoveDown); break;
                }
            };
            aiTimer.Start();
            if(IsGameOver())
            {
                aiTimer.Stop();
                aiTimer = null;
                ExitAiMode();
            }
        }

        private Keys GetBestMove()
        {
            var moves = new (Keys key, Func<bool> func)[]
            {
                (Keys.Left, MoveLeft),
                (Keys.Right, MoveRight),
                (Keys.Up, MoveUp),
                (Keys.Down, MoveDown)
            };

            double bestEval = double.MinValue;
            Keys bestMove = Keys.Left;

            foreach (var (key, func) in moves)
            {
                var backup = (int[,])board.Clone();
                int oldScore = score;

                bool moved = func();
                if (!moved)
                {
                    board = backup;
                    score = oldScore;
                    continue;
                }

                double eval = EvaluateBoard();
                if (eval > bestEval)
                {
                    bestEval = eval;
                    bestMove = key;
                }

                board = backup;
                score = oldScore;
            }

            return bestMove;
        }

        private double EvaluateBoard()
        {
            int empty = 0;
            int max = 0;

            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                {
                    if (board[r, c] == 0) empty++;
                    if (board[r, c] > max) max = board[r, c];
                }

            double scoreEval = empty * 100;

            if (board[3, 0] == max)
                scoreEval += 500;

            return scoreEval;
        }

        private void SaveMenu_Click(object? sender, EventArgs e)
        {
            using var dlg = new SaveFileDialog { Filter = "tar.gz|*.tar.gz" };
            if (dlg.ShowDialog() == DialogResult.OK)
                SaveGameAsTarGz(dlg.FileName);
        }

        private void LoadMenu_Click(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog { Filter = "tar.gz|*.tar.gz" };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                LoadGameFromTarGz(dlg.FileName);
                UpdateUI();
            }
        }

        private void SaveGameAsTarGz(string path)
{
    int[][] jagged = new int[4][];
    for (int r = 0; r < 4; r++)
    {
        jagged[r] = new int[4];
        for (int c = 0; c < 4; c++)
            jagged[r][c] = board[r, c];
    }

    var boardObj = new { board = jagged };
    string boardJson = JsonSerializer.Serialize(boardObj);

    string signature = ComputeHmac(boardJson);

    var finalObj = new
    {
        board = jagged,
        sig = signature
    };

    string finalJson = JsonSerializer.Serialize(finalObj);

    using var fs = File.Create(path);
    using var gz = new GZipStream(fs, CompressionLevel.Optimal);
    using var writer = new TarWriter(gz);

    var entry = new PaxTarEntry(TarEntryType.RegularFile, "save.json");
    entry.DataStream = new MemoryStream(Encoding.UTF8.GetBytes(finalJson));
    writer.WriteEntry(entry);
}

        private void LoadGameFromTarGz(string path)
        {
            using var fs = File.OpenRead(path);
            using var gz = new GZipStream(fs, CompressionMode.Decompress);
            using var reader = new TarReader(gz);

            var entry = reader.GetNextEntry();

            if (entry == null || entry.Name != "save.json")
                throw new InvalidDataException("未找到 save.json");

            using var ms = new MemoryStream();
            entry.DataStream!.CopyTo(ms);

            var json = Encoding.UTF8.GetString(ms.ToArray());
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.GetProperty("board");

            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    board[r, c] = root[r][c].GetInt32();
        }

       private void GameForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Z)
            {
                Undo();
            return;
            }
    if (currentState != GameState.PlayerTurn || isAnimating)
        return;

    switch (e.KeyCode)
    {
        case Keys.Left:
            DoMove(MoveLeft);
            break;

        case Keys.Right:
            DoMove(MoveRight);
            break;

        case Keys.Up:
            DoMove(MoveUp);
            break;

        case Keys.Down:
            DoMove(MoveDown);
            break;
            }
        }
    }
}
