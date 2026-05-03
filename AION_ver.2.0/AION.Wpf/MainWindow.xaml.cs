using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace AION.Wpf;

public sealed partial class MainWindow : Window
{
    private const string DbFileName = "파괴.db";
    private const int FallbackDefaultServerId = 1010;
    private const int FallbackMaxConcurrency = 10;
    private const string ResultFolderName = "결과";
    private const string ResultExcelPrefix = "파괴_";

    private Process? _runningProcess;

    public MainWindow()
    {
        InitializeComponent();

        AppendLog("UI 준비 완료");
        AppendLog($"실행 폴더: {AppContext.BaseDirectory}");
        AppendLog("================================================");
        AppendLog("[ 사용법 ]");
        AppendLog("================================================");
        AppendLog("01) 실행 버튼        : 시작/중지");
        AppendLog("02) 추가/삭제        : 닉네임 단건 추가 또는 삭제");
        AppendLog("03) 레기온 리스트    : 레기온 맴버 닉네임(list.txt) 관리");
        AppendLog("04) 결과 폴더 열기   : 최신 엑셀 결과 확인");
        AppendLog("05) 설정            : 서버코드 / 최대 검색 엔진 개수 변경");
        AppendLog("06) 패키지          : 최초 1회 필수 실행");
        AppendLog("================================================");

        ApplyIdleUiState();
    }

    private async void RunStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_runningProcess is null)
        {
            await RunCollectorAsync();
            return;
        }

        StopCollector();
    }

    private async void InstallBrowserButton_Click(object sender, RoutedEventArgs e)
    {
        await InstallBrowserAsync();
    }

    private async void ManageTargetButton_Click(object sender, RoutedEventArgs e)
    {
        await ManageTargetAsync();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        OpenSettingsDialog();
    }

    private void OpenListButton_Click(object sender, RoutedEventArgs e)
    {
        OpenListFile();
    }

    private void OpenResultFolderButton_Click(object sender, RoutedEventArgs e)
    {
        OpenResultFolder();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ApplyIdleUiState()
    {
        RunStopButton.Content = "실행";
        RunStopButton.IsEnabled = true;
        RunStopButton.Style = (Style)FindResource("PrimaryButtonStyle");
        RunStopButton.Background = CreateBrush("#35C47E");
        RunStopButton.BorderBrush = CreateBrush("#61D99E");
        RunStopButton.Foreground = CreateBrush("#092115");

        InstallBrowserButton.IsEnabled = true;
        ManageTargetButton.IsEnabled = true;
        SettingsButton.IsEnabled = true;
        OpenListButton.IsEnabled = true;
        OpenResultFolderButton.IsEnabled = true;

        StatusTextBlock.Text = "● 대기 중";
        StatusTextBlock.Foreground = CreateBrush("#DDA64E");
    }

    private void ApplyRunningUiState()
    {
        RunStopButton.Content = "중지";
        RunStopButton.IsEnabled = true;
        RunStopButton.Style = (Style)FindResource("StopButtonStyle");
        RunStopButton.Background = CreateBrush("#F07F86");
        RunStopButton.BorderBrush = CreateBrush("#F7A0A5");
        RunStopButton.Foreground = CreateBrush("#2B080B");

        InstallBrowserButton.IsEnabled = false;
        ManageTargetButton.IsEnabled = false;
        SettingsButton.IsEnabled = false;
        OpenListButton.IsEnabled = false;
        OpenResultFolderButton.IsEnabled = false;

        StatusTextBlock.Text = "● 실행 중";
        StatusTextBlock.Foreground = CreateBrush("#69D1A5");
    }

    private void ApplyBusyUiState(string statusText)
    {
        RunStopButton.Content = "실행";
        RunStopButton.IsEnabled = false;
        RunStopButton.Style = (Style)FindResource("PrimaryButtonStyle");
        RunStopButton.Background = CreateBrush("#35C47E");
        RunStopButton.BorderBrush = CreateBrush("#61D99E");
        RunStopButton.Foreground = CreateBrush("#092115");

        InstallBrowserButton.IsEnabled = false;
        ManageTargetButton.IsEnabled = false;
        SettingsButton.IsEnabled = false;
        OpenListButton.IsEnabled = false;
        OpenResultFolderButton.IsEnabled = false;

        StatusTextBlock.Text = $"● {statusText}";
        StatusTextBlock.Foreground = CreateBrush("#E7BE61");
    }

    private static SolidColorBrush CreateBrush(string hex)
    {
        return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    }

    private sealed record SysSettings(int DefaultServerId, int MaxConcurrency);

    private enum TargetAction
    {
        Add,
        Remove
    }

    private sealed record TargetCommandInput(TargetAction Action, string Nickname);

    private enum CollectorComparisonMode
    {
        Database,
        ResultExcel
    }

    private sealed record CollectorRunOptions(CollectorComparisonMode Mode, string? ExcelPath);

    private sealed record ExcelSelectionItem(string FullPath)
    {
        public override string ToString()
        {
            DateTime writtenAt = File.GetLastWriteTime(FullPath);
            return $"{Path.GetFileName(FullPath)}    ({writtenAt:yyyy-MM-dd HH:mm:ss})";
        }
    }
}
