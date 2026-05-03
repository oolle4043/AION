using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.Sqlite;

namespace AION.Wpf;

public sealed partial class MainWindow : Window
{
    private void OpenSettingsDialog()
    {
        if (_runningProcess is not null)
        {
            AppendLog("이미 다른 작업이 실행 중입니다.");
            return;
        }

        string dbPath = ResolveDbPathForSettings();
        SysSettings current = LoadOrCreateSysSettings(dbPath);

        var dialog = new Window
        {
            Title = "설정",
            Width = 380,
            Height = 220
        };

        var root = CreateDarkDialogContent(dialog, "설정");
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(132) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var serverLabel = CreateDialogLabel("서버코드 (나니아: 1010)");
        var serverTextBox = new TextBox { Text = current.DefaultServerId.ToString(), Margin = new Thickness(8, 0, 0, 0) };
        ApplyDialogTextBoxStyle(serverTextBox);

        var concurrencyLabel = CreateDialogLabel("최대 검색 엔진 개수");
        concurrencyLabel.Margin = new Thickness(0, 14, 0, 0);
        var concurrencyTextBox = new TextBox { Text = current.MaxConcurrency.ToString(), Margin = new Thickness(8, 14, 0, 0) };
        ApplyDialogTextBoxStyle(concurrencyTextBox);

        Grid.SetRow(serverLabel, 0);
        Grid.SetColumn(serverLabel, 0);
        Grid.SetRow(serverTextBox, 0);
        Grid.SetColumn(serverTextBox, 1);
        Grid.SetRow(concurrencyLabel, 1);
        Grid.SetColumn(concurrencyLabel, 0);
        Grid.SetRow(concurrencyTextBox, 1);
        Grid.SetColumn(concurrencyTextBox, 1);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 22, 0, 0)
        };

        var okButton = new Button { Content = "확인", Width = 78, Margin = new Thickness(0, 0, 7, 0), IsDefault = true };
        var cancelButton = new Button { Content = "취소", Width = 78, IsCancel = true };
        ApplyDialogButtonStyle(okButton, true);
        ApplyDialogButtonStyle(cancelButton);

        okButton.Click += (_, _) =>
        {
            if (!int.TryParse(serverTextBox.Text.Trim(), out int defaultServerId) || defaultServerId <= 0)
            {
                MessageBox.Show(dialog, "서버코드는 1 이상의 정수여야 합니다.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                serverTextBox.Focus();
                return;
            }

            if (!int.TryParse(concurrencyTextBox.Text.Trim(), out int maxConcurrency) || maxConcurrency <= 0)
            {
                MessageBox.Show(dialog, "최대 검색 엔진 개수는 1 이상의 정수여야 합니다.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                concurrencyTextBox.Focus();
                return;
            }

            SaveSysSettings(dbPath, defaultServerId, maxConcurrency);
            AppendLog($"설정 저장 완료: default_server_id={defaultServerId}, max_concurrency={maxConcurrency}");
            dialog.DialogResult = true;
            dialog.Close();
        };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        Grid.SetRow(buttonPanel, 2);
        Grid.SetColumnSpan(buttonPanel, 2);

        root.Children.Add(serverLabel);
        root.Children.Add(serverTextBox);
        root.Children.Add(concurrencyLabel);
        root.Children.Add(concurrencyTextBox);
        root.Children.Add(buttonPanel);
        dialog.ShowDialog();
    }

    private string ResolveDbPathForSettings()
    {
        string appDir = AppContext.BaseDirectory;
        string exeDbPath = Path.Combine(appDir, DbFileName);
        if (File.Exists(Path.Combine(appDir, "파괴아툴수집엔진.exe")))
        {
            return exeDbPath;
        }

        string? projectDir = FindProjectRootForDev();
        if (projectDir is null)
        {
            return exeDbPath;
        }

        return Path.Combine(projectDir, "bin", "Debug", "net10.0", DbFileName);
    }

    private static SysSettings LoadOrCreateSysSettings(string dbPath)
    {
        string? dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using (var createCommand = connection.CreateCommand())
        {
            createCommand.CommandText =
                """
                CREATE TABLE IF NOT EXISTS SYS (
                    default_server_id INTEGER NOT NULL,
                    max_concurrency INTEGER NOT NULL
                );
                """;
            createCommand.ExecuteNonQuery();
        }

        using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = "SELECT COUNT(*) FROM SYS;";
            long count = (long)(countCommand.ExecuteScalar() ?? 0L);
            if (count == 0)
            {
                using var insertCommand = connection.CreateCommand();
                insertCommand.CommandText =
                    """
                    INSERT INTO SYS (default_server_id, max_concurrency)
                    VALUES ($default_server_id, $max_concurrency);
                    """;
                insertCommand.Parameters.AddWithValue("$default_server_id", FallbackDefaultServerId);
                insertCommand.Parameters.AddWithValue("$max_concurrency", FallbackMaxConcurrency);
                insertCommand.ExecuteNonQuery();
            }
        }

        using var selectCommand = connection.CreateCommand();
        selectCommand.CommandText =
            """
            SELECT default_server_id, max_concurrency
            FROM SYS
            LIMIT 1;
            """;
        using var reader = selectCommand.ExecuteReader();

        if (!reader.Read())
        {
            return new SysSettings(FallbackDefaultServerId, FallbackMaxConcurrency);
        }

        int defaultServerId = reader.GetInt32(0);
        int maxConcurrency = reader.GetInt32(1);

        if (defaultServerId <= 0)
        {
            defaultServerId = FallbackDefaultServerId;
        }

        if (maxConcurrency <= 0)
        {
            maxConcurrency = FallbackMaxConcurrency;
        }

        return new SysSettings(defaultServerId, maxConcurrency);
    }

    private static void SaveSysSettings(string dbPath, int defaultServerId, int maxConcurrency)
    {
        string? dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using var tx = connection.BeginTransaction();

        using (var createCommand = connection.CreateCommand())
        {
            createCommand.Transaction = tx;
            createCommand.CommandText =
                """
                CREATE TABLE IF NOT EXISTS SYS (
                    default_server_id INTEGER NOT NULL,
                    max_concurrency INTEGER NOT NULL
                );
                """;
            createCommand.ExecuteNonQuery();
        }

        using (var clearCommand = connection.CreateCommand())
        {
            clearCommand.Transaction = tx;
            clearCommand.CommandText = "DELETE FROM SYS;";
            clearCommand.ExecuteNonQuery();
        }

        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = tx;
            insertCommand.CommandText =
                """
                INSERT INTO SYS (default_server_id, max_concurrency)
                VALUES ($default_server_id, $max_concurrency);
                """;
            insertCommand.Parameters.AddWithValue("$default_server_id", defaultServerId);
            insertCommand.Parameters.AddWithValue("$max_concurrency", maxConcurrency);
            insertCommand.ExecuteNonQuery();
        }

        tx.Commit();
    }
}
