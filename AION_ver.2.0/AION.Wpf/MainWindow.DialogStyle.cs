using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace AION.Wpf;

public sealed partial class MainWindow
{
    private Grid CreateDarkDialogContent(Window dialog, string title)
    {
        dialog.WindowStyle = WindowStyle.None;
        dialog.AllowsTransparency = true;
        dialog.Background = Brushes.Transparent;
        dialog.ResizeMode = ResizeMode.NoResize;
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        dialog.Owner = this;

        var frame = new Border
        {
            Background = CreateBrush("#172231"),
            BorderBrush = CreateBrush("#5471A8"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            SnapsToDevicePixels = true
        };
        frame.SizeChanged += (_, _) =>
        {
            frame.Clip = new RectangleGeometry(
                new Rect(0, 0, frame.ActualWidth, frame.ActualHeight),
                8,
                8);
        };

        var shell = new Grid();
        shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
        shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid
        {
            Background = CreateBrush("#101927")
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                dialog.DragMove();
            }
        };

        var titleBlock = new TextBlock
        {
            Text = title,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0),
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = CreateBrush("#D9E5F4")
        };

        var closeButton = new Button
        {
            Content = "×",
            Width = 26,
            Height = 26,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            FontWeight = FontWeights.Normal,
            ToolTip = "닫기"
        };
        ApplyDialogCloseButtonStyle(closeButton);
        closeButton.Click += (_, _) => dialog.Close();

        Grid.SetColumn(closeButton, 1);
        header.Children.Add(titleBlock);
        header.Children.Add(closeButton);

        var content = new Grid
        {
            Margin = new Thickness(18)
        };
        Grid.SetRow(content, 1);

        shell.Children.Add(header);
        shell.Children.Add(content);
        frame.Child = shell;
        dialog.Content = frame;

        return content;
    }

    private static void ApplyDialogButtonStyle(Button button, bool isPrimary = false)
    {
        button.Height = 30;
        button.Padding = new Thickness(12, 0, 12, 0);
        button.BorderThickness = new Thickness(1);
        button.FontSize = 11;
        button.FontWeight = FontWeights.Bold;
        button.Cursor = Cursors.Hand;
        button.Background = isPrimary ? CreateBrush("#F3D46B") : CreateBrush("#253449");
        button.BorderBrush = isPrimary ? CreateBrush("#F6DD87") : CreateBrush("#405268");
        button.Foreground = isPrimary ? CreateBrush("#172231") : CreateBrush("#D9E5F4");

        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "Root";
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        presenter.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
        border.AppendChild(presenter);

        var template = new ControlTemplate(typeof(Button))
        {
            VisualTree = border
        };
        button.Template = template;
    }

    private static void ApplyDialogCloseButtonStyle(Button button)
    {
        button.Padding = new Thickness(0);
        button.BorderThickness = new Thickness(1);
        button.Cursor = Cursors.Hand;
        button.Background = CreateBrush("#253449");
        button.BorderBrush = CreateBrush("#405268");
        button.Foreground = CreateBrush("#D9E5F4");

        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "Root";
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        presenter.SetValue(ContentPresenter.MarginProperty, new Thickness(0, -1, 0, 0));
        border.AppendChild(presenter);

        var template = new ControlTemplate(typeof(Button))
        {
            VisualTree = border
        };
        button.Template = template;
    }

    private static void ApplyDialogTextBoxStyle(TextBox textBox)
    {
        textBox.Height = 28;
        textBox.Padding = new Thickness(8, 4, 8, 4);
        textBox.FontSize = 13;
        textBox.Background = CreateBrush("#101927");
        textBox.BorderBrush = CreateBrush("#405268");
        textBox.BorderThickness = new Thickness(1);
        textBox.Foreground = CreateBrush("#D9E5F4");
        textBox.CaretBrush = CreateBrush("#D9E5F4");
    }

    private static ControlTemplate CreateDarkListBoxTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "Root";
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));

        var scrollViewer = new FrameworkElementFactory(typeof(ScrollViewer));
        scrollViewer.SetValue(Control.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        scrollViewer.SetValue(ScrollViewer.CanContentScrollProperty, true);
        scrollViewer.SetValue(ScrollViewer.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));

        var itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
        scrollViewer.AppendChild(itemsPresenter);
        border.AppendChild(scrollViewer);

        return new ControlTemplate(typeof(ListBox))
        {
            VisualTree = border
        };
    }

    private static Style CreateDarkListBoxItemStyle()
    {
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(Control.ForegroundProperty, CreateBrush("#D9E5F4")));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 4, 8, 4)));

        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "Root";
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
        border.AppendChild(presenter);

        var template = new ControlTemplate(typeof(ListBoxItem))
        {
            VisualTree = border
        };

        var selectedTrigger = new Trigger { Property = Selector.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, CreateBrush("#2D4662")));
        template.Triggers.Add(selectedTrigger);

        var disabledTrigger = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabledTrigger.Setters.Add(new Setter(Control.ForegroundProperty, CreateBrush("#9EADC2")));
        template.Triggers.Add(disabledTrigger);

        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        return style;
    }

    private static TextBlock CreateDialogLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = CreateBrush("#D9E5F4")
        };
    }

    private bool ShowDarkConfirmation(string title, string message, string acceptText, string declineText)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 390,
            Height = 210
        };

        var root = CreateDarkDialogContent(dialog, title);
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var messageBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13,
            LineHeight = 20,
            Foreground = CreateBrush("#D9E5F4")
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 20, 0, 0)
        };

        var acceptButton = new Button { Content = acceptText, Width = 96, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var declineButton = new Button { Content = declineText, Width = 96, IsCancel = true };
        ApplyDialogButtonStyle(acceptButton, true);
        ApplyDialogButtonStyle(declineButton);

        bool accepted = false;
        acceptButton.Click += (_, _) =>
        {
            accepted = true;
            dialog.DialogResult = true;
            dialog.Close();
        };

        buttonPanel.Children.Add(acceptButton);
        buttonPanel.Children.Add(declineButton);
        Grid.SetRow(buttonPanel, 1);

        root.Children.Add(messageBlock);
        root.Children.Add(buttonPanel);

        dialog.ShowDialog();
        return accepted;
    }

    private void ShowDarkInfo(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 360,
            Height = 190
        };

        var root = CreateDarkDialogContent(dialog, title);
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var messageBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13,
            LineHeight = 20,
            Foreground = CreateBrush("#D9E5F4")
        };

        var okButton = new Button
        {
            Content = "확인",
            Width = 88,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 20, 0, 0),
            IsDefault = true
        };
        ApplyDialogButtonStyle(okButton, true);
        okButton.Click += (_, _) =>
        {
            dialog.DialogResult = true;
            dialog.Close();
        };

        Grid.SetRow(okButton, 1);
        root.Children.Add(messageBlock);
        root.Children.Add(okButton);

        dialog.ShowDialog();
    }
}
