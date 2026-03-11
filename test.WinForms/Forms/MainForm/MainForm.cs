using System.Diagnostics;
using System.Text;
using System.Drawing.Drawing2D;
using Microsoft.Data.Sqlite;

namespace Test.WinForms;

public sealed partial class MainForm : Form
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
}

