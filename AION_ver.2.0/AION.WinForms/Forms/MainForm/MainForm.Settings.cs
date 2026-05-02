using System.Diagnostics;
using System.Text;
using System.Drawing.Drawing2D;
using Microsoft.Data.Sqlite;

namespace AION.WinForms;

public sealed partial class MainForm : Form
{
    private void OpenSettingsDialog()
    {
        if (_runningProcess is not null)
        {
            AppendLog("이미 다른 작업이 실행 중입니다.");
            return;
        }

        string dbPath = ResolveDbPathForSettings();
        SysSettings current = LoadOrCreateSysSettings(dbPath);

        using var dialog = new Form
        {
            Text = "설정",
            Width = 360,
            Height = 190,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var serverLabel = new Label { Left = 16, Top = 20, Width = 130, Text = "서버코드" };
        var serverTextBox = new TextBox { Left = 150, Top = 16, Width = 170, Text = current.DefaultServerId.ToString() };

        var concurrencyLabel = new Label { Left = 16, Top = 56, Width = 130, Text = "최대 검색 엔진 개수" };
        var concurrencyTextBox = new TextBox { Left = 150, Top = 52, Width = 170, Text = current.MaxConcurrency.ToString() };

        var okButton = new Button { Text = "확인", Left = 164, Top = 100, Width = 75 };
        var cancelButton = new Button { Text = "취소", Left = 245, Top = 100, Width = 75, DialogResult = DialogResult.Cancel };

        okButton.Click += (_, _) =>
        {
            if (!int.TryParse(serverTextBox.Text.Trim(), out int defaultServerId) || defaultServerId <= 0)
            {
                MessageBox.Show("서버코드는 1 이상의 정수여야 합니다.", "입력 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                serverTextBox.Focus();
                return;
            }

            if (!int.TryParse(concurrencyTextBox.Text.Trim(), out int maxConcurrency) || maxConcurrency <= 0)
            {
                MessageBox.Show("최대 검색 엔진 개수는 1 이상의 정수여야 합니다.", "입력 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                concurrencyTextBox.Focus();
                return;
            }

            SaveSysSettings(dbPath, defaultServerId, maxConcurrency);
            AppendLog($"설정 저장 완료: default_server_id={defaultServerId}, max_concurrency={maxConcurrency}");
            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        };

        dialog.Controls.Add(serverLabel);
        dialog.Controls.Add(serverTextBox);
        dialog.Controls.Add(concurrencyLabel);
        dialog.Controls.Add(concurrencyTextBox);
        dialog.Controls.Add(okButton);
        dialog.Controls.Add(cancelButton);
        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;

        dialog.ShowDialog(this);
    }

    private string ResolveDbPathForSettings()
    {
        string appDir = AppContext.BaseDirectory;
        string exeDbPath = Path.Combine(appDir, DbFileName);
        if (File.Exists(Path.Combine(appDir, "파괴아툴수집엔진.exe")))
        {
            return exeDbPath;
        }

        string? projectDir = FindProjectRootForDev();
        if (projectDir is null)
        {
            return exeDbPath;
        }

        return Path.Combine(projectDir, "bin", "Debug", "net10.0", DbFileName);
    }

    private static SysSettings LoadOrCreateSysSettings(string dbPath)
    {
        string? dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using (var createCommand = connection.CreateCommand())
        {
            createCommand.CommandText =
                """
                CREATE TABLE IF NOT EXISTS SYS (
                    default_server_id INTEGER NOT NULL,
                    max_concurrency INTEGER NOT NULL
                );
                """;
            createCommand.ExecuteNonQuery();
        }

        using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = "SELECT COUNT(*) FROM SYS;";
            long count = (long)(countCommand.ExecuteScalar() ?? 0L);
            if (count == 0)
            {
                using var insertCommand = connection.CreateCommand();
                insertCommand.CommandText =
                    """
                    INSERT INTO SYS (default_server_id, max_concurrency)
                    VALUES ($default_server_id, $max_concurrency);
                    """;
                insertCommand.Parameters.AddWithValue("$default_server_id", FallbackDefaultServerId);
                insertCommand.Parameters.AddWithValue("$max_concurrency", FallbackMaxConcurrency);
                insertCommand.ExecuteNonQuery();
            }
        }

        using var selectCommand = connection.CreateCommand();
        selectCommand.CommandText =
            """
            SELECT default_server_id, max_concurrency
            FROM SYS
            LIMIT 1;
            """;
        using var reader = selectCommand.ExecuteReader();

        if (!reader.Read())
        {
            return new SysSettings(FallbackDefaultServerId, FallbackMaxConcurrency);
        }

        int defaultServerId = reader.GetInt32(0);
        int maxConcurrency = reader.GetInt32(1);

        if (defaultServerId <= 0)
        {
            defaultServerId = FallbackDefaultServerId;
        }

        if (maxConcurrency <= 0)
        {
            maxConcurrency = FallbackMaxConcurrency;
        }

        return new SysSettings(defaultServerId, maxConcurrency);
    }

    private static void SaveSysSettings(string dbPath, int defaultServerId, int maxConcurrency)
    {
        string? dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using var tx = connection.BeginTransaction();

        using (var createCommand = connection.CreateCommand())
        {
            createCommand.Transaction = tx;
            createCommand.CommandText =
                """
                CREATE TABLE IF NOT EXISTS SYS (
                    default_server_id INTEGER NOT NULL,
                    max_concurrency INTEGER NOT NULL
                );
                """;
            createCommand.ExecuteNonQuery();
        }

        using (var clearCommand = connection.CreateCommand())
        {
            clearCommand.Transaction = tx;
            clearCommand.CommandText = "DELETE FROM SYS;";
            clearCommand.ExecuteNonQuery();
        }

        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = tx;
            insertCommand.CommandText =
                """
                INSERT INTO SYS (default_server_id, max_concurrency)
                VALUES ($default_server_id, $max_concurrency);
                """;
            insertCommand.Parameters.AddWithValue("$default_server_id", defaultServerId);
            insertCommand.Parameters.AddWithValue("$max_concurrency", maxConcurrency);
            insertCommand.ExecuteNonQuery();
        }

        tx.Commit();
    }

    private ProcessStartInfo? CreateCollectorStartInfo(CollectorRunOptions runOptions)
    {
        string appDir = AppContext.BaseDirectory;
        string collectorExe = Path.Combine(appDir, "파괴아툴수집엔진.exe");
        string arguments = BuildCollectorArguments(runOptions);

        if (File.Exists(collectorExe))
        {
            AppendLog("수집기 실행 방식: 파괴아툴수집엔진.exe 직접 실행");
            return new ProcessStartInfo
            {
                FileName = collectorExe,
                Arguments = arguments,
                WorkingDirectory = appDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
        }

        string? projectDir = FindProjectRootForDev();
        if (projectDir is null)
        {
            return null;
        }

        string projectPath = Path.Combine(projectDir, "AION.Collector.csproj");

        return new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{projectPath}\" -- {arguments}".TrimEnd(),
            WorkingDirectory = projectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
    }

    private static string BuildCollectorArguments(CollectorRunOptions runOptions)
    {
        if (runOptions.Mode != CollectorComparisonMode.ResultExcel || string.IsNullOrWhiteSpace(runOptions.ExcelPath))
        {
            return "";
        }

        string escapedPath = runOptions.ExcelPath.Replace("\"", "\\\"");
        return $"--compare-excel \"{escapedPath}\"";
    }

    private ProcessStartInfo? CreateBrowserInstallStartInfo()
    {
        string appDir = AppContext.BaseDirectory;
        string collectorExe = Path.Combine(appDir, "파괴아툴수집엔진.exe");

        if (File.Exists(collectorExe))
        {
            AppendLog("브라우저 설치 방식: 파괴아툴수집엔진.exe --install-browser");
            return new ProcessStartInfo
            {
                FileName = collectorExe,
                Arguments = "--install-browser",
                WorkingDirectory = appDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
        }

        string? projectDir = FindProjectRootForDev();
        if (projectDir is null)
        {
            return null;
        }

        string projectPath = Path.Combine(projectDir, "AION.Collector.csproj");
        AppendLog("브라우저 설치 방식: dotnet run (개발 모드)");
        return new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{projectPath}\" -- --install-browser",
            WorkingDirectory = projectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
    }

    private ProcessStartInfo? CreateTargetCommandStartInfo(string optionName, string nickname)
    {
        string appDir = AppContext.BaseDirectory;
        string collectorExe = Path.Combine(appDir, "파괴아툴수집엔진.exe");
        string escapedNickname = nickname.Replace("\"", "\\\"");

        if (File.Exists(collectorExe))
        {
            AppendLog($"대상 명령 실행 방식: 파괴아툴수집엔진.exe {optionName}");
            return new ProcessStartInfo
            {
                FileName = collectorExe,
                Arguments = $"{optionName} \"{escapedNickname}\"",
                WorkingDirectory = appDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
        }

        string? projectDir = FindProjectRootForDev();
        if (projectDir is null)
        {
            return null;
        }

        string projectPath = Path.Combine(projectDir, "AION.Collector.csproj");
        AppendLog("대상 명령 실행 방식: dotnet run (개발 모드)");
        return new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{projectPath}\" -- {optionName} \"{escapedNickname}\"",
            WorkingDirectory = projectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
    }
}

