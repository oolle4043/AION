using System.Diagnostics;
using System.Text;
using System.Drawing.Drawing2D;
using Microsoft.Data.Sqlite;

namespace Test.WinForms;

public sealed partial class MainForm : Form
{
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
