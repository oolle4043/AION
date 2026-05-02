using System.Diagnostics;
using System.Text;
using System.Drawing.Drawing2D;
using Microsoft.Data.Sqlite;

namespace AION.WinForms;

public sealed partial class MainForm : Form
{
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
}

