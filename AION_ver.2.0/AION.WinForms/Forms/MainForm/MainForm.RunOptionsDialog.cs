using System.Diagnostics;
using System.Text;
using System.Drawing.Drawing2D;
using Microsoft.Data.Sqlite;

namespace AION.WinForms;

public sealed partial class MainForm : Form
{
    private const string ResultFolderName = "결과";
    private const string ResultExcelPrefix = "파괴_";

    private CollectorRunOptions? PromptCollectorRunOptions()
    {
        string resultDir = ResolveResultDirectory();
        List<string> excelPaths = Directory.Exists(resultDir)
            ? Directory.GetFiles(resultDir, $"{ResultExcelPrefix}*.xlsx")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToList()
            : new List<string>();

        using var dialog = new Form
        {
            Text = "비교 기준 선택",
            Width = 520,
            Height = 390,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var dbRadio = new RadioButton
        {
            Left = 20,
            Top = 20,
            Width = 220,
            Text = "DB와 비교",
            Checked = true
        };

        var excelRadio = new RadioButton
        {
            Left = 20,
            Top = 50,
            Width = 220,
            Text = "결과 엑셀과 비교"
        };

        var guideLabel = new Label
        {
            Left = 20,
            Top = 84,
            Width = 460,
            Height = 20,
            Text = "결과 엑셀 비교를 선택하면 아래 목록에서 기준 파일을 선택하세요."
        };

        var excelList = new ListBox
        {
            Left = 20,
            Top = 110,
            Width = 460,
            Height = 180,
            Enabled = false
        };

        foreach (string excelPath in excelPaths)
        {
            excelList.Items.Add(new ExcelSelectionItem(excelPath));
        }

        if (excelList.Items.Count > 0)
        {
            excelList.SelectedIndex = 0;
        }

        var emptyLabel = new Label
        {
            Left = 20,
            Top = 298,
            Width = 460,
            Height = 20,
            ForeColor = Color.FromArgb(160, 64, 64),
            Text = excelPaths.Count == 0 ? "결과 폴더에 비교 가능한 엑셀이 없습니다. DB 비교만 사용할 수 있습니다." : ""
        };

        var okButton = new Button { Text = "실행", Left = 322, Top = 324, Width = 75 };
        var cancelButton = new Button { Text = "취소", Left = 405, Top = 324, Width = 75, DialogResult = DialogResult.Cancel };

        void ApplySelectionState()
        {
            bool useExcel = excelRadio.Checked;
            excelList.Enabled = useExcel && excelPaths.Count > 0;
        }

        dbRadio.CheckedChanged += (_, _) => ApplySelectionState();
        excelRadio.CheckedChanged += (_, _) => ApplySelectionState();

        CollectorRunOptions? selected = null;
        okButton.Click += (_, _) =>
        {
            if (dbRadio.Checked)
            {
                selected = new CollectorRunOptions(CollectorComparisonMode.Database, null);
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
                return;
            }

            if (excelPaths.Count == 0)
            {
                MessageBox.Show("선택할 결과 엑셀이 없습니다. DB 비교를 사용하세요.", "선택 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (excelList.SelectedItem is not ExcelSelectionItem excelItem)
            {
                MessageBox.Show("비교할 결과 엑셀을 선택하세요.", "선택 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                excelList.Focus();
                return;
            }

            selected = new CollectorRunOptions(CollectorComparisonMode.ResultExcel, excelItem.FullPath);
            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        };

        dialog.Controls.Add(dbRadio);
        dialog.Controls.Add(excelRadio);
        dialog.Controls.Add(guideLabel);
        dialog.Controls.Add(excelList);
        dialog.Controls.Add(emptyLabel);
        dialog.Controls.Add(okButton);
        dialog.Controls.Add(cancelButton);
        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;
        dialog.Shown += (_, _) => ApplySelectionState();

        return dialog.ShowDialog(this) == DialogResult.OK ? selected : null;
    }

    private string ResolveResultDirectory()
    {
        string appDir = AppContext.BaseDirectory;
        string exeCollectorPath = Path.Combine(appDir, "파괴아툴수집엔진.exe");
        if (File.Exists(exeCollectorPath))
        {
            return Path.Combine(appDir, ResultFolderName);
        }

        string? projectDir = FindProjectRootForDev();
        if (projectDir is null)
        {
            return Path.Combine(appDir, ResultFolderName);
        }

        return Path.Combine(projectDir, "bin", "Debug", "net10.0", ResultFolderName);
    }

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
