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
            Height = 220,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            Background = Brushes.White
        };

        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var titleLabel = new TextBlock
        {
            Text = "닉네임 입력",
            FontWeight = FontWeights.Bold,
            Foreground = CreateBrush("#404040")
        };

        var nicknameTextBox = new TextBox
        {
            Margin = new Thickness(0, 12, 0, 0),
            FontSize = 14
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 24, 0, 0)
        };

        var addButton = new Button { Content = "추가", Width = 78, Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
        var removeButton = new Button { Content = "삭제", Width = 78, Margin = new Thickness(0, 0, 6, 0) };
        var cancelButton = new Button { Content = "취소", Width = 78, IsCancel = true };

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
        dialog.Content = root;
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
            Height = 390,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            Background = Brushes.White
        };

        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var dbRadio = new RadioButton
        {
            Content = "DB와 비교",
            IsChecked = true
        };

        var excelRadio = new RadioButton
        {
            Content = "결과 엑셀과 비교",
            Margin = new Thickness(0, 8, 0, 0)
        };

        var guideLabel = new TextBlock
        {
            Margin = new Thickness(0, 16, 0, 0),
            Text = "결과 엑셀 비교를 선택하면 아래 목록에서 기준 파일을 선택하세요."
        };

        var excelList = new ListBox
        {
            Margin = new Thickness(0, 12, 0, 0),
            MinHeight = 180,
            IsEnabled = false
        };

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
            Foreground = CreateBrush("#A04040"),
            Text = excelPaths.Count == 0 ? "결과 폴더에 비교 가능한 엑셀이 없습니다. DB 비교만 사용할 수 있습니다." : ""
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };

        var okButton = new Button { Content = "실행", Width = 75, Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
        var cancelButton = new Button { Content = "취소", Width = 75, IsCancel = true };

        void ApplySelectionState()
        {
            bool useExcel = excelRadio.IsChecked == true;
            excelList.IsEnabled = useExcel && excelPaths.Count > 0;
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
        dialog.Content = root;
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
