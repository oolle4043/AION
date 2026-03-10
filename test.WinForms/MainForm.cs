using System.Diagnostics;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Test.WinForms;

public sealed class MainForm : Form
{
    private const string DbFileName = "파괴.db";
    private const int FallbackDefaultServerId = 1010;
    private const int FallbackMaxConcurrency = 10;

    private readonly Button _runStopButton;
    private readonly Button _installBrowserButton;
    private readonly Button _manageTargetButton;
    private readonly Button _settingsButton;
    private readonly Button _openListButton;
    private readonly Button _openResultFolderButton;
    private readonly TextBox _logBox;
    private readonly Label _statusLabel;
    private Process? _runningProcess;

    public MainForm()
    {
        Text = "파괴 아툴 기록";
        Width = 900;
        Height = 650;
        StartPosition = FormStartPosition.CenterScreen;

        var topContainer = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            Padding = new Padding(8)
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        var statusPanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 150
        };

        _runStopButton = new Button { Text = "실행", Width = 90, Height = 30 };
        _runStopButton.Click += async (_, _) => await HandleRunStopClickAsync();

        _installBrowserButton = new Button { Text = "필수패키지 설치", Width = 125, Height = 30 };
        _installBrowserButton.Click += async (_, _) => await InstallBrowserAsync();

        _manageTargetButton = new Button { Text = "추가/삭제", Width = 100, Height = 30 };
        _manageTargetButton.Click += async (_, _) => await ManageTargetAsync();

        _settingsButton = new Button { Text = "설정", Width = 90, Height = 30 };
        _settingsButton.Click += (_, _) => OpenSettingsDialog();

        _openListButton = new Button { Text = "레기온 리스트", Width = 110, Height = 30 };
        _openListButton.Click += (_, _) => OpenListFile();

        _openResultFolderButton = new Button { Text = "결과 폴더 열기", Width = 130, Height = 30 };
        _openResultFolderButton.Click += (_, _) => OpenResultFolder();

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            BorderStyle = BorderStyle.FixedSingle
        };

        buttonPanel.Controls.Add(_runStopButton);
        buttonPanel.Controls.Add(_manageTargetButton);
        buttonPanel.Controls.Add(_openListButton);
        buttonPanel.Controls.Add(_settingsButton);
        buttonPanel.Controls.Add(_installBrowserButton);
        buttonPanel.Controls.Add(_openResultFolderButton);

        statusPanel.Controls.Add(_statusLabel);
        topContainer.Controls.Add(buttonPanel);
        topContainer.Controls.Add(statusPanel);

        _logBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            Font = new Font("Consolas", 10),
            WordWrap = false
        };

        Controls.Add(_logBox);
        Controls.Add(topContainer);

        AppendLog("UI 준비 완료");
        AppendLog($"실행 폴더: {AppContext.BaseDirectory}");
        ApplyIdleUiState();
    }

    private async Task HandleRunStopClickAsync()
    {
        if (_runningProcess is null)
        {
            await RunCollectorAsync();
            return;
        }

        StopCollector();
    }

    private void ApplyIdleUiState()
    {
        _runStopButton.Text = "실행";
        _runStopButton.Enabled = true;
        _runStopButton.UseVisualStyleBackColor = false;
        _runStopButton.BackColor = Color.FromArgb(219, 238, 255);

        _installBrowserButton.Enabled = true;
        _manageTargetButton.Enabled = true;
        _settingsButton.Enabled = true;
        _openListButton.Enabled = true;
        _openResultFolderButton.Enabled = true;

        _statusLabel.Text = "대기 중";
        _statusLabel.BackColor = Color.FromArgb(232, 232, 232);
        _statusLabel.ForeColor = Color.FromArgb(52, 52, 52);
    }

    private void ApplyRunningUiState()
    {
        _runStopButton.Text = "중지";
        _runStopButton.Enabled = true;
        _runStopButton.UseVisualStyleBackColor = false;
        _runStopButton.BackColor = Color.FromArgb(255, 215, 215);

        _installBrowserButton.Enabled = false;
        _manageTargetButton.Enabled = false;
        _settingsButton.Enabled = false;
        _openListButton.Enabled = false;
        _openResultFolderButton.Enabled = false;

        _statusLabel.Text = "실행 중";
        _statusLabel.BackColor = Color.FromArgb(214, 245, 214);
        _statusLabel.ForeColor = Color.FromArgb(24, 97, 28);
    }

    private void ApplyBusyUiState(string statusText)
    {
        _runStopButton.Text = "실행";
        _runStopButton.Enabled = false;
        _runStopButton.UseVisualStyleBackColor = false;
        _runStopButton.BackColor = Color.FromArgb(219, 238, 255);

        _installBrowserButton.Enabled = false;
        _manageTargetButton.Enabled = false;
        _settingsButton.Enabled = false;
        _openListButton.Enabled = false;
        _openResultFolderButton.Enabled = false;

        _statusLabel.Text = statusText;
        _statusLabel.BackColor = Color.FromArgb(255, 236, 204);
        _statusLabel.ForeColor = Color.FromArgb(122, 67, 0);
    }

    private async Task RunCollectorAsync()
    {
        if (_runningProcess is not null)
        {
            AppendLog("이미 실행 중입니다.");
            return;
        }

        ProcessStartInfo? psi = CreateCollectorStartInfo();
        if (psi is null)
        {
            AppendLog("수집기 실행 대상을 찾지 못했습니다. 파괴아툴수집엔진.exe를 UI와 같은 폴더에 배치하세요.");
            return;
        }

        _runningProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _runningProcess.OutputDataReceived += (_, e) => { if (e.Data is not null) AppendLog(e.Data); };
        _runningProcess.ErrorDataReceived += (_, e) => { if (e.Data is not null) AppendLog("[ERR] " + e.Data); };

        ApplyRunningUiState();
        AppendLog($"실행 시작: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        _runningProcess.Start();
        _runningProcess.BeginOutputReadLine();
        _runningProcess.BeginErrorReadLine();

        await _runningProcess.WaitForExitAsync();

        AppendLog($"실행 종료 (ExitCode={_runningProcess.ExitCode})");
        _runningProcess.Dispose();
        _runningProcess = null;
        ApplyIdleUiState();
    }

    private async Task InstallBrowserAsync()
    {
        if (_runningProcess is not null)
        {
            AppendLog("이미 다른 작업이 실행 중입니다.");
            return;
        }

        ProcessStartInfo? psi = CreateBrowserInstallStartInfo();
        if (psi is null)
        {
            AppendLog("브라우저 설치 실행 대상을 찾지 못했습니다.");
            return;
        }

        _runningProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _runningProcess.OutputDataReceived += (_, e) => { if (e.Data is not null) AppendLog(e.Data); };
        _runningProcess.ErrorDataReceived += (_, e) => { if (e.Data is not null) AppendLog("[ERR] " + e.Data); };

        ApplyBusyUiState("설치 중");
        AppendLog("브라우저 설치 시작");

        _runningProcess.Start();
        _runningProcess.BeginOutputReadLine();
        _runningProcess.BeginErrorReadLine();
        await _runningProcess.WaitForExitAsync();

        AppendLog($"브라우저 설치 종료 (ExitCode={_runningProcess.ExitCode})");
        _runningProcess.Dispose();
        _runningProcess = null;
        ApplyIdleUiState();
    }

    private async Task ManageTargetAsync()
    {
        if (_runningProcess is not null)
        {
            AppendLog("이미 다른 작업이 실행 중입니다.");
            return;
        }

        TargetAction? action = PromptTargetAction();
        if (action is null)
        {
            return;
        }

        bool isAdd = action == TargetAction.Add;
        string title = isAdd ? "추가할 닉네임" : "삭제할 닉네임";
        string? nickname = PromptNickname(title);
        if (string.IsNullOrWhiteSpace(nickname))
        {
            AppendLog("닉네임 입력이 비어 작업을 취소했습니다.");
            return;
        }

        if (isAdd)
        {
            await ExecuteTargetCommandAsync("--add-target", nickname.Trim(), "추가/조회 중", "대상 추가 및 단건 조회");
            return;
        }

        await ExecuteTargetCommandAsync("--remove-target", nickname.Trim(), "삭제 중", "대상 삭제");
    }

    private async Task ExecuteTargetCommandAsync(
        string optionName,
        string nickname,
        string statusText,
        string taskName)
    {
        ProcessStartInfo? psi = CreateTargetCommandStartInfo(optionName, nickname);
        if (psi is null)
        {
            AppendLog($"{taskName} 실행 대상을 찾지 못했습니다.");
            return;
        }

        _runningProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _runningProcess.OutputDataReceived += (_, e) => { if (e.Data is not null) AppendLog(e.Data); };
        _runningProcess.ErrorDataReceived += (_, e) => { if (e.Data is not null) AppendLog("[ERR] " + e.Data); };

        ApplyBusyUiState(statusText);
        AppendLog($"{taskName} 시작: {nickname}");

        _runningProcess.Start();
        _runningProcess.BeginOutputReadLine();
        _runningProcess.BeginErrorReadLine();
        await _runningProcess.WaitForExitAsync();

        AppendLog($"{taskName} 종료 (ExitCode={_runningProcess.ExitCode})");
        _runningProcess.Dispose();
        _runningProcess = null;
        ApplyIdleUiState();
    }

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

    private ProcessStartInfo? CreateCollectorStartInfo()
    {
        string appDir = AppContext.BaseDirectory;
        string collectorExe = Path.Combine(appDir, "파괴아툴수집엔진.exe");

        if (File.Exists(collectorExe))
        {
            AppendLog("수집기 실행 방식: 파괴아툴수집엔진.exe 직접 실행");
            return new ProcessStartInfo
            {
                FileName = collectorExe,
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

        string projectPath = Path.Combine(projectDir, "test.csproj");
        AppendLog("수집기 실행 방식: dotnet run (개발 모드)");
        return new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{projectPath}\"",
            WorkingDirectory = projectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
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

        string projectPath = Path.Combine(projectDir, "test.csproj");
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

        string projectPath = Path.Combine(projectDir, "test.csproj");
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

    private static TargetAction? PromptTargetAction()
    {
        using var dialog = new Form
        {
            Text = "추가/삭제 선택",
            Width = 360,
            Height = 140,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var addButton = new Button { Text = "추가", Left = 36, Top = 40, Width = 80 };
        var removeButton = new Button { Text = "삭제", Left = 134, Top = 40, Width = 80 };
        var cancelButton = new Button { Text = "취소", Left = 232, Top = 40, Width = 80 };

        TargetAction? selected = null;
        addButton.Click += (_, _) =>
        {
            selected = TargetAction.Add;
            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        };
        removeButton.Click += (_, _) =>
        {
            selected = TargetAction.Remove;
            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        };
        cancelButton.Click += (_, _) =>
        {
            dialog.DialogResult = DialogResult.Cancel;
            dialog.Close();
        };

        dialog.Controls.Add(addButton);
        dialog.Controls.Add(removeButton);
        dialog.Controls.Add(cancelButton);
        dialog.CancelButton = cancelButton;

        return dialog.ShowDialog() == DialogResult.OK ? selected : null;
    }

    private static string? PromptNickname(string title)
    {
        using var dialog = new Form
        {
            Text = title,
            Width = 380,
            Height = 150,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var textBox = new TextBox
        {
            Left = 16,
            Top = 16,
            Width = 330
        };

        var okButton = new Button
        {
            Text = "확인",
            Left = 190,
            Top = 52,
            Width = 75,
            DialogResult = DialogResult.OK
        };

        var cancelButton = new Button
        {
            Text = "취소",
            Left = 271,
            Top = 52,
            Width = 75,
            DialogResult = DialogResult.Cancel
        };

        dialog.Controls.Add(textBox);
        dialog.Controls.Add(okButton);
        dialog.Controls.Add(cancelButton);
        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;

        return dialog.ShowDialog() == DialogResult.OK ? textBox.Text : null;
    }

    private enum TargetAction
    {
        Add,
        Remove
    }

    private void StopCollector()
    {
        if (_runningProcess is null)
        {
            return;
        }

        try
        {
            _runningProcess.Kill(entireProcessTree: true);
            AppendLog("실행 중지 요청 완료");
        }
        catch (Exception ex)
        {
            AppendLog("중지 실패: " + ex.Message);
        }
    }

    private void OpenListFile()
    {
        string listPath = Path.Combine(AppContext.BaseDirectory, "list.txt");
        if (!File.Exists(listPath))
        {
            File.WriteAllText(listPath, "", Encoding.UTF8);
            AppendLog("list.txt가 없어 새 파일을 생성했습니다.");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "notepad.exe",
            Arguments = $"\"{listPath}\"",
            UseShellExecute = true
        });
    }

    private void OpenResultFolder()
    {
        string resultDir = Path.Combine(AppContext.BaseDirectory, "결과");
        Directory.CreateDirectory(resultDir);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = resultDir,
            UseShellExecute = true
        });
    }

    private static string? FindProjectRootForDev()
    {
        string current = AppContext.BaseDirectory;

        for (int i = 0; i < 10; i++)
        {
            if (File.Exists(Path.Combine(current, "test.csproj")))
            {
                return current;
            }

            string? parent = Directory.GetParent(current)?.FullName;
            if (parent is null)
            {
                break;
            }

            current = parent;
        }

        return null;
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(message));
            return;
        }

        _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private sealed record SysSettings(int DefaultServerId, int MaxConcurrency);
}
