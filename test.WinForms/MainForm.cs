using System.Diagnostics;
using System.Text;
using System.Drawing.Drawing2D;
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
    private readonly Button _statusButton;
    private Process? _runningProcess;

    public MainForm()
    {
        Text = "파괴 아툴 수집기";
        Width = 1020;
        Height = 700;
        MinimumSize = new Size(980, 620);
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
            Width = 190,
            Padding = new Padding(20, 4, 8, 4),
            BackColor = Color.FromArgb(245, 246, 248)
        };

        _runStopButton = new Button { Text = "실행", Width = 90, Height = 30 };
        _runStopButton.Click += async (_, _) => await HandleRunStopClickAsync();

        _installBrowserButton = new Button { Text = "필수패키지 설치", Width = 158, Height = 30 };
        _installBrowserButton.Click += async (_, _) => await InstallBrowserAsync();
        _installBrowserButton.Margin = new Padding(3, 3, 16, 3);

        _manageTargetButton = new Button { Text = "추가/삭제", Width = 100, Height = 30 };
        _manageTargetButton.Click += async (_, _) => await ManageTargetAsync();

        _settingsButton = new Button { Text = "설정", Width = 90, Height = 30 };
        _settingsButton.Click += (_, _) => OpenSettingsDialog();

        _openListButton = new Button { Text = "레기온 리스트", Width = 125, Height = 30 };
        _openListButton.Click += (_, _) => OpenListFile();

        _openResultFolderButton = new Button { Text = "결과 폴더 열기", Width = 130, Height = 30 };
        _openResultFolderButton.Click += (_, _) => OpenResultFolder();

        _statusButton = new Button
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            TabStop = false,
            UseVisualStyleBackColor = false,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Default
        };
        _statusButton.FlatAppearance.BorderSize = 0;
        _statusButton.BackColor = statusPanel.BackColor;
        _statusButton.Click += (_, _) => { };

        buttonPanel.Controls.Add(_runStopButton);
        buttonPanel.Controls.Add(_manageTargetButton);
        buttonPanel.Controls.Add(_openListButton);
        buttonPanel.Controls.Add(_openResultFolderButton);
        buttonPanel.Controls.Add(_settingsButton);
        buttonPanel.Controls.Add(_installBrowserButton);

        statusPanel.Controls.Add(_statusButton);
        topContainer.Controls.Add(buttonPanel);
        topContainer.Controls.Add(statusPanel);

        var footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 24,
            BackColor = Color.FromArgb(246, 246, 248),
            Padding = new Padding(0, 2, 8, 2)
        };
        var authorLabel = new Label
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            Text = "Made by CJJ",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
            ForeColor = Color.FromArgb(112, 112, 120),
            TextAlign = ContentAlignment.MiddleRight
        };
        footerPanel.Controls.Add(authorLabel);

        _logBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            Font = new Font("Consolas", 10),
            WordWrap = false,
            BorderStyle = BorderStyle.FixedSingle,
            TabStop = false
        };

        Controls.Add(_logBox);
        Controls.Add(footerPanel);
        Controls.Add(topContainer);

        AppendLog("UI 준비 완료");
        AppendLog($"실행 폴더: {AppContext.BaseDirectory}");

        AppendLog("================================================");
        AppendLog("[ 사용법 ]");
        AppendLog("================================================");
        AppendLog("01) 실행 버튼     : 시작/중지");
        AppendLog("02) 추가/삭제     : 닉네임 단건 추가 또는 삭제");
        AppendLog("03) 레기온 리스트  : 레기온 맴버 닉네임(list.txt) 관리");
        AppendLog("04) 결과 폴더 열기 : 최신 엑셀 결과 확인");
        AppendLog("05) 설정         : 서버코드 / 최대 검색 엔진 개수 변경");
        AppendLog("06) 필수패키지 설치 : 최초 1회 필수 실행");
        AppendLog("07) 상태창        : 실행중 또는 대기중 표시   ");
        AppendLog("================================================");

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

        _statusButton.Text = "● 대기 중";
        _statusButton.ForeColor = Color.FromArgb(76, 88, 102);
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

        _statusButton.Text = "● 실행 중";
        _statusButton.ForeColor = Color.FromArgb(35, 122, 62);
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

        _statusButton.Text = $"● {statusText}";
        _statusButton.ForeColor = Color.FromArgb(138, 88, 24);
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

        TargetCommandInput? commandInput = PromptTargetCommand();
        if (commandInput is null)
        {
            return;
        }

        if (commandInput.Action == TargetAction.Add)
        {
            await ExecuteTargetCommandAsync("--add-target", commandInput.Nickname, "추가/조회 중", "대상 추가 및 단건 조회");
            return;
        }

        await ExecuteTargetCommandAsync("--remove-target", commandInput.Nickname, "삭제 중", "대상 삭제");
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

    private static TargetCommandInput? PromptTargetCommand()
    {
        using var dialog = new Form
        {
            Text = "추가/삭제",
            Width = 430,
            Height = 220,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var titleLabel = new Label
        {
            Left = 20,
            Top = 20,
            Width = 380,
            Height = 20,
            Text = "닉네임 입력",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.FromArgb(64, 64, 64)
        };
        var nicknameTextBox = new TextBox
        {
            Left = 20,
            Top = 50,
            Width = 380,
            Height = 30,
            Font = new Font("Segoe UI", 11, FontStyle.Regular)
        };

        var buttonPanel = new Panel
        {
            Left = 20,
            Top = 112,
            Width = 380,
            Height = 44
        };

        var addButton = new Button { Text = "추가", Left = 68, Top = 6, Width = 78, Height = 30 };
        var removeButton = new Button { Text = "삭제", Left = 151, Top = 6, Width = 78, Height = 30 };
        var cancelButton = new Button { Text = "취소", Left = 234, Top = 6, Width = 78, Height = 30 };

        addButton.UseVisualStyleBackColor = true;
        removeButton.UseVisualStyleBackColor = true;
        cancelButton.UseVisualStyleBackColor = true;

        TargetCommandInput? selected = null;
        addButton.Click += (_, _) =>
        {
            string nickname = nicknameTextBox.Text.Trim();
            if (nickname.Length == 0)
            {
                MessageBox.Show("닉네임을 입력하세요.", "입력 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                nicknameTextBox.Focus();
                return;
            }

            selected = new TargetCommandInput(TargetAction.Add, nickname);
            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        };
        removeButton.Click += (_, _) =>
        {
            string nickname = nicknameTextBox.Text.Trim();
            if (nickname.Length == 0)
            {
                MessageBox.Show("닉네임을 입력하세요.", "입력 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                nicknameTextBox.Focus();
                return;
            }

            selected = new TargetCommandInput(TargetAction.Remove, nickname);
            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        };
        cancelButton.Click += (_, _) =>
        {
            dialog.DialogResult = DialogResult.Cancel;
            dialog.Close();
        };

        buttonPanel.Controls.Add(addButton);
        buttonPanel.Controls.Add(removeButton);
        buttonPanel.Controls.Add(cancelButton);

        dialog.Controls.Add(titleLabel);
        dialog.Controls.Add(nicknameTextBox);
        dialog.Controls.Add(buttonPanel);
        dialog.AcceptButton = addButton;
        dialog.CancelButton = cancelButton;
        dialog.Shown += (_, _) =>
        {
            ApplyRoundedRegion(nicknameTextBox, 8);
            nicknameTextBox.Focus();
        };

        return dialog.ShowDialog() == DialogResult.OK ? selected : null;
    }
    private static void ApplyRoundedRegion(Control control, int radius)
    {
        int diameter = radius * 2;
        var rect = new Rectangle(0, 0, control.Width, control.Height);
        using var path = new GraphicsPath();
        path.StartFigure();
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        control.Region = new Region(path);
    }
    private enum TargetAction
    {
        Add,
        Remove
    }

    private sealed record TargetCommandInput(TargetAction Action, string Nickname);

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

