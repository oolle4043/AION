using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AION.Wpf;

public sealed partial class MainWindow : Window
{
    private TargetCommandInput? PromptTargetCommand()
    {
        var dialog = new Window
        {
            Title = "추가/삭제",
            Width = 430,
            Height = 220
        };

        var root = CreateDarkDialogContent(dialog, "추가/삭제");
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var titleLabel = CreateDialogLabel("닉네임 입력");

        var nicknameTextBox = new TextBox
        {
            Margin = new Thickness(0, 12, 0, 0)
        };
        ApplyDialogTextBoxStyle(nicknameTextBox);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 24, 0, 0)
        };

        var addButton = new Button { Content = "추가", Width = 78, Margin = new Thickness(0, 0, 7, 0), IsDefault = true };
        var removeButton = new Button { Content = "삭제", Width = 78, Margin = new Thickness(0, 0, 7, 0) };
        var cancelButton = new Button { Content = "취소", Width = 78, IsCancel = true };
        ApplyDialogButtonStyle(addButton, true);
        ApplyDialogButtonStyle(removeButton);
        ApplyDialogButtonStyle(cancelButton);

        TargetCommandInput? selected = null;

        void Submit(TargetAction action)
        {
            string nickname = nicknameTextBox.Text.Trim();
            if (nickname.Length == 0)
            {
                MessageBox.Show(dialog, "닉네임을 입력하세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                nicknameTextBox.Focus();
                return;
            }

            selected = new TargetCommandInput(action, nickname);
            dialog.DialogResult = true;
            dialog.Close();
        }

        addButton.Click += (_, _) => Submit(TargetAction.Add);
        removeButton.Click += (_, _) => Submit(TargetAction.Remove);

        Grid.SetRow(titleLabel, 0);
        Grid.SetRow(nicknameTextBox, 1);
        Grid.SetRow(buttonPanel, 2);

        buttonPanel.Children.Add(addButton);
        buttonPanel.Children.Add(removeButton);
        buttonPanel.Children.Add(cancelButton);

        root.Children.Add(titleLabel);
        root.Children.Add(nicknameTextBox);
        root.Children.Add(buttonPanel);
        dialog.Loaded += (_, _) => nicknameTextBox.Focus();

        return dialog.ShowDialog() == true ? selected : null;
    }

    private CollectorRunOptions? PromptCollectorRunOptions()
    {
        string resultDir = ResolveResultDirectory();
        List<string> excelPaths = Directory.Exists(resultDir)
            ? Directory.GetFiles(resultDir, $"{ResultExcelPrefix}*.xlsx")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToList()
            : new List<string>();

        var dialog = new Window
        {
            Title = "비교 기준 선택",
            Width = 520,
            Height = 390
        };

        var root = CreateDarkDialogContent(dialog, "비교 기준 선택");
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var dbRadio = new RadioButton
        {
            Content = "DB와 비교",
            IsChecked = true,
            Foreground = CreateBrush("#D9E5F4"),
            FontSize = 12,
            FontWeight = FontWeights.Bold
        };

        var excelRadio = new RadioButton
        {
            Content = "결과 엑셀과 비교",
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = CreateBrush("#D9E5F4"),
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            IsEnabled = excelPaths.Count > 0
        };

        var guideLabel = new TextBlock
        {
            Margin = new Thickness(0, 16, 0, 0),
            Text = "결과 엑셀 비교를 선택하면 아래 목록에서 기준 파일을 선택하세요.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = excelPaths.Count > 0 ? CreateBrush("#AFC0D6") : CreateBrush("#6F7F94"),
            FontSize = 12
        };

        var excelList = new ListBox
        {
            Margin = new Thickness(0, 12, 0, 0),
            MinHeight = 180,
            IsEnabled = false,
            Background = CreateBrush("#1C2A3A"),
            BorderBrush = CreateBrush("#405268"),
            BorderThickness = new Thickness(1),
            Foreground = CreateBrush("#D9E5F4"),
            FontSize = 11,
            Padding = new Thickness(4)
        };
        excelList.Template = CreateDarkListBoxTemplate();
        excelList.ItemContainerStyle = CreateDarkListBoxItemStyle();
        excelList.Resources.Add(SystemColors.ControlBrushKey, CreateBrush("#1C2A3A"));
        excelList.Resources.Add(SystemColors.ControlTextBrushKey, CreateBrush("#D9E5F4"));
        excelList.Resources.Add(SystemColors.WindowBrushKey, CreateBrush("#1C2A3A"));
        excelList.Resources.Add(SystemColors.GrayTextBrushKey, CreateBrush("#9EADC2"));

        foreach (string excelPath in excelPaths)
        {
            excelList.Items.Add(new ExcelSelectionItem(excelPath));
        }

        if (excelList.Items.Count > 0)
        {
            excelList.SelectedIndex = 0;
        }

        var emptyLabel = new TextBlock
        {
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = CreateBrush("#F07F86"),
            FontSize = 12,
            Text = excelPaths.Count == 0 ? "결과 폴더에 비교 가능한 엑셀이 없습니다. DB 비교만 사용할 수 있습니다." : ""
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };

        var okButton = new Button { Content = "실행", Width = 78, Margin = new Thickness(0, 0, 7, 0), IsDefault = true };
        var cancelButton = new Button { Content = "취소", Width = 78, IsCancel = true };
        ApplyDialogButtonStyle(okButton, true);
        ApplyDialogButtonStyle(cancelButton);

        void ApplySelectionState()
        {
            bool useExcel = excelRadio.IsChecked == true;
            excelList.IsEnabled = useExcel && excelPaths.Count > 0;
            excelRadio.Foreground = excelPaths.Count > 0 ? CreateBrush("#D9E5F4") : CreateBrush("#6F7F94");
            guideLabel.Foreground = excelPaths.Count > 0 ? CreateBrush("#AFC0D6") : CreateBrush("#6F7F94");
        }

        dbRadio.Checked += (_, _) => ApplySelectionState();
        excelRadio.Checked += (_, _) => ApplySelectionState();

        CollectorRunOptions? selected = null;
        okButton.Click += (_, _) =>
        {
            if (dbRadio.IsChecked == true)
            {
                selected = new CollectorRunOptions(CollectorComparisonMode.Database, null);
                dialog.DialogResult = true;
                dialog.Close();
                return;
            }

            if (excelPaths.Count == 0)
            {
                MessageBox.Show(dialog, "선택할 결과 엑셀이 없습니다. DB 비교를 사용하세요.", "선택 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (excelList.SelectedItem is not ExcelSelectionItem excelItem)
            {
                MessageBox.Show(dialog, "비교할 결과 엑셀을 선택하세요.", "선택 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                excelList.Focus();
                return;
            }

            selected = new CollectorRunOptions(CollectorComparisonMode.ResultExcel, excelItem.FullPath);
            dialog.DialogResult = true;
            dialog.Close();
        };

        Grid.SetRow(dbRadio, 0);
        Grid.SetRow(excelRadio, 1);
        Grid.SetRow(guideLabel, 2);
        Grid.SetRow(excelList, 3);
        Grid.SetRow(emptyLabel, 4);
        Grid.SetRow(buttonPanel, 5);

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        root.Children.Add(dbRadio);
        root.Children.Add(excelRadio);
        root.Children.Add(guideLabel);
        root.Children.Add(excelList);
        root.Children.Add(emptyLabel);
        root.Children.Add(buttonPanel);
        dialog.Loaded += (_, _) => ApplySelectionState();

        return dialog.ShowDialog() == true ? selected : null;
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
}
