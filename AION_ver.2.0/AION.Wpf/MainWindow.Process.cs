using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace AION.Wpf;

public sealed partial class MainWindow : Window
{
    private async Task RunCollectorAsync()
    {
        if (_runningProcess is not null)
        {
            AppendLog("이미 실행 중입니다.");
            return;
        }

        CollectorRunOptions? runOptions = PromptCollectorRunOptions();
        if (runOptions is null)
        {
            AppendLog("실행이 취소되었습니다.");
            return;
        }

        ProcessStartInfo? psi = CreateCollectorStartInfo(runOptions);
        if (psi is null)
        {
            AppendLog("수집기 실행 대상을 찾지 못했습니다. 파괴아툴수집엔진.exe를 UI와 같은 폴더에 배치하세요.");
            return;
        }

        _runningProcess = CreateTrackedProcess(psi);
        ApplyRunningUiState();
        AppendLog($"실행 시작: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        AppendLog(runOptions.Mode == CollectorComparisonMode.Database
            ? "비교 기준 선택: DB"
            : $"비교 기준 선택: 결과 엑셀 ({Path.GetFileName(runOptions.ExcelPath)})");

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

        _runningProcess = CreateTrackedProcess(psi);
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

    private async Task ExecuteTargetCommandAsync(string optionName, string nickname, string statusText, string taskName)
    {
        ProcessStartInfo? psi = CreateTargetCommandStartInfo(optionName, nickname);
        if (psi is null)
        {
            AppendLog($"{taskName} 실행 대상을 찾지 못했습니다.");
            return;
        }

        _runningProcess = CreateTrackedProcess(psi);
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

    private Process CreateTrackedProcess(ProcessStartInfo psi)
    {
        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                AppendLog(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                AppendLog("[ERR] " + e.Data);
            }
        };
        return process;
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
