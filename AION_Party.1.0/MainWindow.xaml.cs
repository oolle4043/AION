using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AION.Party;

public sealed partial class MainWindow : Window
{
    private readonly string _listPath;
    private readonly string _resultPath;
    private int _mainCountPerParty = 1;
    private int _altCountPerParty = 3;

    public MainWindow()
    {
        InitializeComponent();

        _listPath = Path.Combine(AppContext.BaseDirectory, "list.txt");
        string currentListPath = Path.Combine(Environment.CurrentDirectory, "list.txt");
        if (File.Exists(currentListPath))
        {
            _listPath = currentListPath;
        }

        _resultPath = Path.Combine(Path.GetDirectoryName(_listPath) ?? AppContext.BaseDirectory, "party_result.txt");
        LoadListPreview();
        LoadResultPreview();
        SetStatus("\uB300\uAE30 \uC911", "#DDA64E");
    }

    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            GenerateButton.IsEnabled = false;
            SetStatus("\uC0DD\uC131\uC911", "#69D1A5", "");

            PartyGenerationOptions options = ReadOptions();
            PartyGenerationResult result = await Task.Run(() => PartyGenerator.Generate(_listPath, options));
            ResultTextBox.Text = result.Report;
            SetStatus("\uC0DD\uC131\uC644\uB8CC", "#69D1A5", result.ResultPath);
        }
        catch (Exception ex)
        {
            ResultTextBox.Text = ex.Message;
            SetStatus("\uC624\uB958 \uBC1C\uC0DD", "#F07F86", "");
            MessageBox.Show(this, ex.Message, "AION Party", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            GenerateButton.IsEnabled = true;
        }
    }

    private PartyGenerationOptions ReadOptions()
    {
        return new PartyGenerationOptions(_mainCountPerParty, _altCountPerParty);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        PartySettings? settings = ShowSettingsDialog();
        if (settings is null)
        {
            return;
        }

        _mainCountPerParty = settings.MainCountPerParty;
        _altCountPerParty = settings.AltCountPerParty;
        SetStatus($"\uC124\uC815: \uBCF8{_mainCountPerParty} / \uBD80{_altCountPerParty}", "#DDA64E");
    }

    private PartySettings? ShowSettingsDialog()
    {
        Window dialog = new()
        {
            Title = "\uD30C\uD2F0 \uAD6C\uC131",
            Owner = this,
            Width = 360,
            Height = 250,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent
        };

        Border frame = new()
        {
            Background = CreateBrush("#172231"),
            BorderBrush = CreateBrush("#405268"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8)
        };

        Grid root = new()
        {
            Margin = new Thickness(22, 20, 22, 18)
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        TextBlock title = new()
        {
            Text = "\uD30C\uD2F0 \uAD6C\uC131",
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = CreateBrush("#D9E5F4")
        };

        TextBlock guide = new()
        {
            Text = "\uC608: \uBCF81 / \uBD803",
            Margin = new Thickness(0, 6, 0, 14),
            FontSize = 12,
            Foreground = CreateBrush("#8392A8")
        };

        Grid form = new();
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        TextBlock mainLabel = CreateDialogLabel("\uBCF8\uCE90 \uC218");
        TextBlock altLabel = CreateDialogLabel("\uBD80\uCE90 \uC218");
        TextBox mainTextBox = CreateDialogTextBox(_mainCountPerParty.ToString());
        TextBox altTextBox = CreateDialogTextBox(_altCountPerParty.ToString());

        mainTextBox.IsEnabled = false;
        mainTextBox.ToolTip = "\uD604\uC7AC \uD30C\uD2F0 \uD3B8\uC131 \uC5D4\uC9C4\uC740 \uBCF81 \uAE30\uC900\uC785\uB2C8\uB2E4.";

        Grid.SetRow(mainLabel, 0);
        Grid.SetColumn(mainLabel, 0);
        Grid.SetRow(mainTextBox, 0);
        Grid.SetColumn(mainTextBox, 1);
        Grid.SetRow(altLabel, 1);
        Grid.SetColumn(altLabel, 0);
        Grid.SetRow(altTextBox, 1);
        Grid.SetColumn(altTextBox, 1);

        form.Children.Add(mainLabel);
        form.Children.Add(mainTextBox);
        form.Children.Add(altLabel);
        form.Children.Add(altTextBox);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        Button okButton = CreateDialogButton("\uC800\uC7A5", isPrimary: true);
        Button cancelButton = CreateDialogButton("\uCDE8\uC18C");
        cancelButton.IsCancel = true;

        PartySettings? selected = null;
        okButton.Click += (_, _) =>
        {
            if (!int.TryParse(mainTextBox.Text.Trim(), out int mainCount) || mainCount != 1)
            {
                MessageBox.Show(dialog, "\uBCF8\uCE90 \uC218\uB294 \uD604\uC7AC 1\uB9CC \uC0AC\uC6A9\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.", "\uC785\uB825 \uC624\uB958", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(altTextBox.Text.Trim(), out int altCount) || altCount is < 1 or > 3)
            {
                MessageBox.Show(dialog, "\uBD80\uCE90 \uC218\uB294 1\uBD80\uD130 3\uAE4C\uC9C0 \uC785\uB825\uD558\uC138\uC694.", "\uC785\uB825 \uC624\uB958", MessageBoxButton.OK, MessageBoxImage.Warning);
                altTextBox.Focus();
                return;
            }

            selected = new PartySettings(mainCount, altCount);
            dialog.DialogResult = true;
            dialog.Close();
        };

        cancelButton.Click += (_, _) =>
        {
            dialog.DialogResult = false;
            dialog.Close();
        };

        buttons.Children.Add(okButton);
        buttons.Children.Add(cancelButton);

        Grid.SetRow(title, 0);
        Grid.SetRow(guide, 1);
        Grid.SetRow(form, 2);
        Grid.SetRow(buttons, 4);

        root.Children.Add(title);
        root.Children.Add(guide);
        root.Children.Add(form);
        root.Children.Add(buttons);
        frame.Child = root;
        dialog.Content = frame;

        return dialog.ShowDialog() == true ? selected : null;
    }

    private void OpenListButton_Click(object sender, RoutedEventArgs e)
    {
        EnsureListFile();
        OpenPath(_listPath);
        LoadListPreview();
    }

    private void OpenResultButton_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(_resultPath))
        {
            MessageBox.Show(
                this,
                "\uC544\uC9C1 \uACB0\uACFC \uD30C\uC77C\uC774 \uC5C6\uC2B5\uB2C8\uB2E4. \uBA3C\uC800 \uD30C\uD2F0 \uC0DD\uC131\uC744 \uC2E4\uD589\uD558\uC138\uC694.",
                "AION Party",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        OpenPath(_resultPath);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void LoadListPreview()
    {
        EnsureListFile();
        ListPreviewTextBox.Text = File.ReadAllText(_listPath, Encoding.UTF8);
    }

    private void LoadResultPreview()
    {
        ResultTextBox.Text = File.Exists(_resultPath)
            ? File.ReadAllText(_resultPath, Encoding.UTF8)
            : "";
    }

    private void EnsureListFile()
    {
        if (File.Exists(_listPath))
        {
            return;
        }

        File.WriteAllText(_listPath, "", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void OpenPath(string path)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = path,
            UseShellExecute = true
        };
        Process.Start(startInfo);
    }

    private void SetStatus(string text, string color, string? resultPath = null)
    {
        StatusTextBlock.Text = text;
        StatusTextBlock.Foreground = CreateBrush(color);

        if (resultPath is not null)
        {
            ResultPathTextBlock.Text = resultPath;
            ResultPathTextBlock.ToolTip = resultPath;
        }
    }

    private static TextBlock CreateDialogLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            Margin = new Thickness(0, 0, 10, 10),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = CreateBrush("#AFC0D6")
        };
    }

    private static TextBox CreateDialogTextBox(string text)
    {
        return new TextBox
        {
            Text = text,
            Height = 28,
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(8, 4, 8, 4),
            Background = CreateBrush("#1C2A3A"),
            BorderBrush = CreateBrush("#405268"),
            Foreground = CreateBrush("#D9E5F4"),
            CaretBrush = CreateBrush("#D9E5F4"),
            FontSize = 12
        };
    }

    private static Button CreateDialogButton(string text, bool isPrimary = false)
    {
        Button button = new()
        {
            Content = text,
            Width = 92,
            Height = 30,
            Margin = new Thickness(0, 0, 8, 0),
            Background = CreateBrush(isPrimary ? "#35C47E" : "#253449"),
            BorderBrush = CreateBrush(isPrimary ? "#61D99E" : "#405268"),
            Foreground = CreateBrush(isPrimary ? "#092115" : "#D9E5F4"),
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        button.Template = CreateRoundedButtonTemplate();
        return button;
    }

    private static ControlTemplate CreateRoundedButtonTemplate()
    {
        FrameworkElementFactory border = new(typeof(Border))
        {
            Name = "Root"
        };
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));

        FrameworkElementFactory presenter = new(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);

        return new ControlTemplate(typeof(Button))
        {
            VisualTree = border
        };
    }

    private static SolidColorBrush CreateBrush(string color)
    {
        return (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
    }

    private sealed record PartySettings(int MainCountPerParty, int AltCountPerParty);
}
