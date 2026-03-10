using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.Playwright;

class Program
{
    private const int FallbackDefaultServerId = 1010;
    private const int FallbackMaxConcurrency = 10;
    private const string DbFileName = "파괴.db";
    private const string CsvFilePrefix = "파괴";
    private const string ResultFolderName = "결과";
    private static readonly string[] PreferredJobOrder = new[]
    {
        "검성", "수호성", "살성", "마도성", "궁성", "정령성", "호법성", "치유성"
    };
    private static readonly TimeZoneInfo KstTimeZone = ResolveKstTimeZone();
    private static readonly StringComparer KoreanSortComparer = StringComparer.Create(new CultureInfo("ko-KR"), ignoreCase: false);

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Any(x => string.Equals(x, "--install-browser", StringComparison.OrdinalIgnoreCase)))
        {
            int installExitCode = InstallChromium();
            Environment.ExitCode = installExitCode;
            return;
        }
        if (TryGetOptionValue(args, "--add-target", out string addTargetName))
        {
            Environment.ExitCode = await AddTargetAndFetchAsync(addTargetName);
            return;
        }
        if (TryGetOptionValue(args, "--remove-target", out string removeTargetName))
        {
            Environment.ExitCode = RemoveTargetEverywhere(removeTargetName);
            return;
        }

        string appDir = AppContext.BaseDirectory;

        string listPath = Path.Combine(appDir, "list.txt");

        if (!File.Exists(listPath))
        {
            Console.WriteLine("list.txt 파일을 찾을 수 없습니다.");
            return;
        }

        string dbPath = Path.Combine(appDir, DbFileName);
        AppSettings settings = EnsureAndLoadSettings(dbPath);

        List<CharacterTarget> targets = ReadTargets(listPath, settings.DefaultServerId);
        if (targets.Count == 0)
        {
            Console.WriteLine("list.txt에 조회할 항목이 없습니다.");
            return;
        }

        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                            "(KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"
            });

            await context.RouteAsync("**/*", async route =>
            {
                string resourceType = route.Request.ResourceType;
                if (resourceType == "image" || resourceType == "media" || resourceType == "font")
                {
                    await route.AbortAsync();
                    return;
                }
                await route.ContinueAsync();
            });

            CharacterResult[] results = new CharacterResult[targets.Count];
            int concurrency = Math.Min(settings.MaxConcurrency, targets.Count);
            using var semaphore = new System.Threading.SemaphoreSlim(concurrency, concurrency);

            Task[] tasks = targets.Select(async (target, index) =>
            {
                await semaphore.WaitAsync();
                try
                {
                    results[index] = await FetchCharacterAsync(context, target);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            await Task.WhenAll(tasks);
            await context.CloseAsync();

            SaveResultsToSqlite(dbPath, results, writeCsv: true);

            foreach (CharacterResult result in results)
            {
                Console.WriteLine("--------------------------------------------------");
                if (!string.IsNullOrEmpty(result.Error))
                {
                    Console.WriteLine($"조회 실패: {result.Error}");
                    continue;
                }

                Console.WriteLine($"닉네임: {result.Nickname}");
                Console.WriteLine($"직업: {result.Job}");
                Console.WriteLine($"전투력: {result.Power}");
                Console.WriteLine($"아툴 점수: {result.Score}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("브라우저 초기화 오류가 발생했습니다.");
            Console.WriteLine(ex.Message);
        }
    }

    static bool TryGetOptionValue(string[] args, string optionName, out string value)
    {
        value = "";

        for (int i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], optionName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 >= args.Length)
            {
                return false;
            }

            value = args[i + 1].Trim();
            return value.Length > 0;
        }

        return false;
    }

    static async Task<int> AddTargetAndFetchAsync(string rawName)
    {
        string nickname = RemoveParenthesizedSections(rawName).Trim();
        if (nickname.Length == 0)
        {
            Console.WriteLine("닉네임이 비어 있어 추가를 중단합니다.");
            return 1;
        }

        string appDir = AppContext.BaseDirectory;
        string listPath = Path.Combine(appDir, "list.txt");
        string dbPath = Path.Combine(appDir, DbFileName);
        AppSettings settings = EnsureAndLoadSettings(dbPath);
        CharacterTarget target = new CharacterTarget(settings.DefaultServerId, nickname);

        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                            "(KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"
            });

            await context.RouteAsync("**/*", async route =>
            {
                string resourceType = route.Request.ResourceType;
                if (resourceType == "image" || resourceType == "media" || resourceType == "font")
                {
                    await route.AbortAsync();
                    return;
                }
                await route.ContinueAsync();
            });

            CharacterResult result = await FetchCharacterAsync(context, target);
            await context.CloseAsync();

            if (!string.IsNullOrEmpty(result.Error))
            {
                Console.WriteLine($"단건 조회 실패: {result.Error}");
                return 1;
            }

            SaveResultsToSqlite(dbPath, new[] { result }, writeCsv: false);
            UpsertTargetInListByJob(
                listPath,
                result.Nickname,
                target.ServerId,
                result.Job,
                dbPath);
            UpdateLatestExcelForUpsert(result.Nickname, result.Job, result.Power, result.Score, dbPath);
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine($"닉네임: {result.Nickname}");
            Console.WriteLine($"직업: {result.Job}");
            Console.WriteLine($"전투력: {result.Power}");
            Console.WriteLine($"아툴 점수: {result.Score}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("단건 조회 중 오류가 발생했습니다.");
            Console.WriteLine(ex.Message);
            return 1;
        }
    }

    static int RemoveTargetEverywhere(string rawName)
    {
        string nickname = RemoveParenthesizedSections(rawName).Trim();
        if (nickname.Length == 0)
        {
            Console.WriteLine("닉네임이 비어 있어 삭제를 중단합니다.");
            return 1;
        }

        string appDir = AppContext.BaseDirectory;
        string listPath = Path.Combine(appDir, "list.txt");
        string dbPath = Path.Combine(appDir, DbFileName);

        bool removedFromList = RemoveTargetFromList(listPath, nickname);
        bool removedFromDb = RemoveTargetFromDb(dbPath, nickname);
        if (removedFromDb || removedFromList)
        {
            UpdateLatestExcelForRemove(nickname, dbPath);
        }

        Console.WriteLine($"list.txt 삭제: {(removedFromList ? "완료" : "대상 없음")}");
        Console.WriteLine($"DB 삭제: {(removedFromDb ? "완료" : "대상 없음")}");
        return 0;
    }

    static int InstallChromium()
    {
        try
        {
            Console.WriteLine("Playwright Chromium 설치를 시작합니다...");
            int exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
            if (exitCode == 0)
            {
                Console.WriteLine("Playwright Chromium 설치가 완료되었습니다.");
            }
            else
            {
                Console.WriteLine($"Playwright Chromium 설치 실패 (ExitCode={exitCode})");
            }

            return exitCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Playwright Chromium 설치 중 오류가 발생했습니다.");
            Console.WriteLine(ex.Message);
            return 1;
        }
    }

    static AppSettings EnsureAndLoadSettings(string dbPath)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using (var createSysCommand = connection.CreateCommand())
        {
            createSysCommand.CommandText =
                """
                CREATE TABLE IF NOT EXISTS SYS (
                    default_server_id INTEGER NOT NULL,
                    max_concurrency INTEGER NOT NULL
                );
                """;
            createSysCommand.ExecuteNonQuery();
        }

        using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = "SELECT COUNT(*) FROM SYS;";
            long count = (long)(countCommand.ExecuteScalar() ?? 0L);
            if (count == 0)
            {
                using var insertDefaultCommand = connection.CreateCommand();
                insertDefaultCommand.CommandText =
                    """
                    INSERT INTO SYS (default_server_id, max_concurrency)
                    VALUES ($default_server_id, $max_concurrency);
                    """;
                insertDefaultCommand.Parameters.AddWithValue("$default_server_id", FallbackDefaultServerId);
                insertDefaultCommand.Parameters.AddWithValue("$max_concurrency", FallbackMaxConcurrency);
                insertDefaultCommand.ExecuteNonQuery();
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
            return new AppSettings(FallbackDefaultServerId, FallbackMaxConcurrency);
        }

        int defaultServerId = reader.GetInt32(0);
        int maxConcurrency = reader.GetInt32(1);

        if (maxConcurrency < 1)
        {
            maxConcurrency = 1;
        }

        Console.WriteLine($"설정 로드: DefaultServerId={defaultServerId}, MaxConcurrency={maxConcurrency}");
        return new AppSettings(defaultServerId, maxConcurrency);
    }

    static void SaveResultsToSqlite(string dbPath, IEnumerable<CharacterResult> results, bool writeCsv)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        EnsureCharacterResultsSchema(connection);
        EnsureJobCountsSchema(connection);
        Dictionary<string, CharacterSnapshot> previousSnapshotByNickname = LoadCharacterSnapshots(connection);

        using var transaction = connection.BeginTransaction();
        using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText =
            """
            INSERT INTO CHARACTER_RESULTS (nickname, job, power, atul_score, updated_at)
            VALUES ($nickname, $job, $power, $atul_score, $updated_at)
            ON CONFLICT(nickname) DO UPDATE SET
                job = excluded.job,
                power = excluded.power,
                atul_score = excluded.atul_score,
                updated_at = excluded.updated_at;
            """;

        var nicknameParam = insertCommand.CreateParameter();
        nicknameParam.ParameterName = "$nickname";
        insertCommand.Parameters.Add(nicknameParam);

        var jobParam = insertCommand.CreateParameter();
        jobParam.ParameterName = "$job";
        insertCommand.Parameters.Add(jobParam);

        var powerParam = insertCommand.CreateParameter();
        powerParam.ParameterName = "$power";
        insertCommand.Parameters.Add(powerParam);

        var scoreParam = insertCommand.CreateParameter();
        scoreParam.ParameterName = "$atul_score";
        insertCommand.Parameters.Add(scoreParam);

        var updatedAtParam = insertCommand.CreateParameter();
        updatedAtParam.ParameterName = "$updated_at";
        insertCommand.Parameters.Add(updatedAtParam);

        int savedCount = 0;
        var latestResults = new List<CharacterResult>();
        foreach (CharacterResult result in results)
        {
            if (!string.IsNullOrEmpty(result.Error))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(result.Nickname))
            {
                continue;
            }

            nicknameParam.Value = result.Nickname;
            jobParam.Value = result.Job;
            powerParam.Value = result.Power;
            scoreParam.Value = result.Score;
            updatedAtParam.Value = GetKstNowString();
            insertCommand.ExecuteNonQuery();
            savedCount++;
            latestResults.Add(result);
        }

        transaction.Commit();
        RefreshJobCounts(connection);
        Dictionary<string, int> jobTotals = LoadJobTotals(connection);
        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine($"DB 저장 완료: {savedCount}건 ({dbPath})");
        if (!writeCsv)
        {
            return;
        }

        List<ComparisonRow> excelRows = BuildComparisonRows(latestResults, previousSnapshotByNickname);
        string excelPath = BuildTimestampedExcelPath();
        string writtenExcelPath = WriteComparisonExcel(excelPath, excelRows, jobTotals);
        Console.WriteLine($"엑셀 생성 완료: {excelRows.Count}건 ({writtenExcelPath})");
    }

    static Dictionary<string, CharacterSnapshot> LoadCharacterSnapshots(SqliteConnection connection)
    {
        var map = new Dictionary<string, CharacterSnapshot>(StringComparer.Ordinal);

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT nickname, job, power, atul_score
            FROM CHARACTER_RESULTS;
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            string nickname = reader.IsDBNull(0) ? "" : reader.GetString(0);
            if (string.IsNullOrWhiteSpace(nickname))
            {
                continue;
            }

            string job = reader.IsDBNull(1) ? "" : reader.GetString(1);
            string power = reader.IsDBNull(2) ? "" : reader.GetString(2);
            string atulScore = reader.IsDBNull(3) ? "" : reader.GetString(3);
            map[nickname] = new CharacterSnapshot(job, power, atulScore);
        }

        return map;
    }

    static Dictionary<string, int> LoadJobTotals(SqliteConnection connection)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT job, job_count
            FROM JOB_COUNTS;
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            string job = reader.IsDBNull(0) ? "" : reader.GetString(0);
            int count = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            if (!string.IsNullOrWhiteSpace(job))
            {
                map[job] = count;
            }
        }

        return map;
    }

    static List<ComparisonRow> BuildComparisonRows(
        IEnumerable<CharacterResult> latestResults,
        Dictionary<string, CharacterSnapshot> previousSnapshotByNickname)
    {
        var rows = new List<ComparisonRow>();

        foreach (CharacterResult latest in latestResults)
        {
            CharacterSnapshot baseline;
            if (!previousSnapshotByNickname.TryGetValue(latest.Nickname, out CharacterSnapshot? existing))
            {
                baseline = new CharacterSnapshot(latest.Job, latest.Power, latest.Score);
            }
            else
            {
                baseline = existing;
            }

            string compareJob = latest.Job;
            if (string.IsNullOrWhiteSpace(compareJob))
            {
                compareJob = baseline.Job;
            }

            string displayJob = baseline.Job;
            if (string.IsNullOrWhiteSpace(displayJob))
            {
                displayJob = compareJob;
            }

            string levelDelta = CalculateDelta(baseline.Power, latest.Power);
            string atulDelta = CalculateDelta(baseline.AtulScore, latest.Score);

            rows.Add(new ComparisonRow(
                latest.Nickname,
                displayJob,
                baseline.Power,
                baseline.AtulScore,
                latest.Power,
                latest.Score,
                levelDelta,
                atulDelta));
        }

        return rows;
    }

    static string WriteComparisonExcel(
        string excelPath,
        IEnumerable<ComparisonRow> rows,
        Dictionary<string, int> jobTotals)
    {
        List<string> totalLines = BuildJobTotalLines(jobTotals);
        List<ComparisonRow> rowList = rows
            .OrderBy(x => NormalizeJobName(x.Job), KoreanSortComparer)
            .ThenBy(x => x.Nickname, KoreanSortComparer)
            .ToList();
        string[] pastelRainbow = new[]
        {
            "#F8B4B4",
            "#FFD6A5",
            "#FFF3B0",
            "#CDEAC0",
            "#BDE0FE",
            "#BEE9E8",
            "#CDB4DB",
            "#EFC3E6"
        };
        const string statRangeColorHex = "#DCEAF7";      // E~H
        const string deltaRangeColorHex = "#E7F7D4";     // I~J default
        const string negativeDeltaColorHex = "#F9C6C6";  // I~J negative
        var jobColorMap = rowList
            .Select(x => NormalizeJobName(x.Job))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, KoreanSortComparer)
            .Select((job, index) => new { job, color = pastelRainbow[index % pastelRainbow.Length] })
            .ToDictionary(x => x.job, x => x.color, StringComparer.Ordinal);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("결과");
        string[] headers = new[]
        {
            "",
            "번호",
            "파괴(닉네임)",
            "직업",
            "레벨",
            "아툴",
            "레벨(비교)",
            "아툴(비교)",
            "레벨 상승량",
            "아툴 상승량",
            "직업 (total)"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        for (int i = 0; i < rowList.Count; i++)
        {
            ComparisonRow row = rowList[i];
            string totalCell = i < totalLines.Count ? totalLines[i] : "";
            int rowIndex = i + 2;
            sheet.Cell(rowIndex, 2).Value = i + 1;
            sheet.Cell(rowIndex, 2).Style.NumberFormat.Format = "#,##0";
            sheet.Cell(rowIndex, 3).Value = row.Nickname;
            sheet.Cell(rowIndex, 4).Value = row.Job;
            SetNumericCellValue(sheet.Cell(rowIndex, 5), row.Level);
            SetNumericCellValue(sheet.Cell(rowIndex, 6), row.Atul);
            SetNumericCellValue(sheet.Cell(rowIndex, 7), row.LevelCompare);
            SetNumericCellValue(sheet.Cell(rowIndex, 8), row.AtulCompare);
            SetNumericCellValue(sheet.Cell(rowIndex, 9), row.LevelDelta);
            SetNumericCellValue(sheet.Cell(rowIndex, 10), row.AtulDelta);
            SetNumericCellValue(sheet.Cell(rowIndex, 11), totalCell);

            sheet.Range(rowIndex, 5, rowIndex, 8).Style.Fill.BackgroundColor = XLColor.FromHtml(statRangeColorHex);
            sheet.Range(rowIndex, 9, rowIndex, 10).Style.Fill.BackgroundColor = XLColor.FromHtml(deltaRangeColorHex);

            if (TryParseSignedLong(row.LevelDelta, out long levelDelta) && levelDelta < 0)
            {
                sheet.Cell(rowIndex, 9).Style.Fill.BackgroundColor = XLColor.FromHtml(negativeDeltaColorHex);
            }

            if (TryParseSignedLong(row.AtulDelta, out long atulDelta) && atulDelta < 0)
            {
                sheet.Cell(rowIndex, 10).Style.Fill.BackgroundColor = XLColor.FromHtml(negativeDeltaColorHex);
            }

            string normalizedJob = NormalizeJobName(row.Job);
            if (jobColorMap.TryGetValue(normalizedJob, out string? hexColor))
            {
                sheet.Range(rowIndex, 2, rowIndex, 4).Style.Fill.BackgroundColor = XLColor.FromHtml(hexColor);
            }

            string? totalHexColor = GetTotalCellColor(totalLines, i, jobColorMap);
            if (!string.IsNullOrEmpty(totalHexColor))
            {
                sheet.Cell(rowIndex, 11).Style.Fill.BackgroundColor = XLColor.FromHtml(totalHexColor);
            }
        }

        for (int i = rowList.Count; i < totalLines.Count; i++)
        {
            int rowIndex = i + 2;
            SetNumericCellValue(sheet.Cell(rowIndex, 11), totalLines[i]);
            string? totalHexColor = GetTotalCellColor(totalLines, i, jobColorMap);
            if (!string.IsNullOrEmpty(totalHexColor))
            {
                sheet.Cell(rowIndex, 11).Style.Fill.BackgroundColor = XLColor.FromHtml(totalHexColor);
            }
        }

        sheet.Columns(2, 11).AdjustToContents();
        for (int col = 2; col <= 11; col++)
        {
            sheet.Column(col).Width += 1.5;
        }
        ApplyMinimumColumnWidths(sheet);
        workbook.SaveAs(excelPath);
        return excelPath;
    }

    static string BuildTimestampedExcelPath()
    {
        string resultDir = Path.Combine(AppContext.BaseDirectory, ResultFolderName);
        Directory.CreateDirectory(resultDir);
        string fileName = $"{CsvFilePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return Path.Combine(resultDir, fileName);
    }

    static List<string> BuildJobTotalLines(Dictionary<string, int> jobTotals)
    {
        var lines = new List<string>();
        var normalizedTotals = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var pair in jobTotals)
        {
            string key = NormalizeJobName(pair.Key);
            if (key.Length == 0)
            {
                continue;
            }

            if (normalizedTotals.TryGetValue(key, out int existing))
            {
                normalizedTotals[key] = existing + pair.Value;
            }
            else
            {
                normalizedTotals[key] = pair.Value;
            }
        }

        foreach (string job in PreferredJobOrder)
        {
            string normalizedJob = NormalizeJobName(job);
            if (!normalizedTotals.TryGetValue(normalizedJob, out int count))
            {
                continue;
            }

            lines.Add(normalizedJob);
            lines.Add(count.ToString(CultureInfo.InvariantCulture));
        }

        var otherJobs = normalizedTotals
            .Where(x => !string.Equals(x.Key, "TOTAL", StringComparison.OrdinalIgnoreCase)
                        && !PreferredJobOrder.Any(p => string.Equals(NormalizeJobName(p), x.Key, StringComparison.Ordinal)))
            .OrderBy(x => x.Key, StringComparer.Ordinal);

        foreach (var pair in otherJobs)
        {
            lines.Add(pair.Key);
            lines.Add(pair.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (normalizedTotals.TryGetValue("TOTAL", out int totalCount))
        {
            lines.Add("total");
            lines.Add(totalCount.ToString(CultureInfo.InvariantCulture));
        }

        return lines;
    }

    static void SetNumericCellValue(IXLCell cell, string rawValue)
    {
        if (TryParseSignedLong(rawValue, out long parsed))
        {
            cell.Value = parsed;
            cell.Style.NumberFormat.Format = "#,##0";
            return;
        }

        cell.Value = rawValue;
    }

    static void ApplyMinimumColumnWidths(IXLWorksheet sheet)
    {
        var minWidths = new Dictionary<int, double>
        {
            [2] = 5,   // 번호
            [3] = 16,  // 파괴(닉네임)
            [4] = 8,   // 직업
            [5] = 9,   // 레벨
            [6] = 11,  // 아툴
            [7] = 12,  // 레벨(비교)
            [8] = 12,  // 아툴(비교)
            [9] = 11,  // 레벨 상승량
            [10] = 11, // 아툴 상승량
            [11] = 11  // 직업(total)
        };

        foreach (var pair in minWidths)
        {
            var col = sheet.Column(pair.Key);
            if (col.Width < pair.Value)
            {
                col.Width = pair.Value;
            }
        }
    }

    static string? GetTotalCellColor(
        List<string> totalLines,
        int index,
        Dictionary<string, string> jobColorMap)
    {
        const string totalSummaryColorHex = "#FDE2E4";

        if (index < 0 || index >= totalLines.Count)
        {
            return null;
        }

        string current = totalLines[index].Trim();
        if (string.Equals(current, "total", StringComparison.OrdinalIgnoreCase))
        {
            return totalSummaryColorHex;
        }

        if (jobColorMap.TryGetValue(NormalizeJobName(current), out string? directColor))
        {
            return directColor;
        }

        if (TryParseSignedLong(current, out _) && index > 0)
        {
            string upper = totalLines[index - 1].Trim();
            if (string.Equals(upper, "total", StringComparison.OrdinalIgnoreCase))
            {
                return totalSummaryColorHex;
            }

            if (jobColorMap.TryGetValue(NormalizeJobName(upper), out string? pairColor))
            {
                return pairColor;
            }
        }

        return null;
    }

    static string NormalizeJobName(string job)
    {
        string normalized = (job ?? "").Trim();
        if (normalized.Length == 0)
        {
            return "-";
        }

        if (string.Equals(normalized, "수호", StringComparison.Ordinal)) return "수호성";
        if (string.Equals(normalized, "마도", StringComparison.Ordinal)) return "마도성";
        if (string.Equals(normalized, "정령", StringComparison.Ordinal)) return "정령성";
        if (string.Equals(normalized, "호법", StringComparison.Ordinal)) return "호법성";
        if (string.Equals(normalized, "치유", StringComparison.Ordinal)) return "치유성";
        if (string.Equals(normalized, "TOTAL", StringComparison.OrdinalIgnoreCase)) return "TOTAL";
        return normalized;
    }

    static int GetJobSortIndex(string job)
    {
        string normalized = NormalizeJobName(job);
        for (int i = 0; i < PreferredJobOrder.Length; i++)
        {
            if (string.Equals(NormalizeJobName(PreferredJobOrder[i]), normalized, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    static string CalculateDelta(string oldValue, string newValue)
    {
        if (!TryParseSignedLong(oldValue, out long oldNumber))
        {
            oldNumber = 0;
        }

        if (!TryParseSignedLong(newValue, out long newNumber))
        {
            newNumber = 0;
        }

        long diff = newNumber - oldNumber;
        return diff.ToString(CultureInfo.InvariantCulture);
    }

    static bool TryParseSignedLong(string value, out long parsed)
    {
        parsed = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Replace(",", "").Trim();
        if (long.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
        {
            return true;
        }

        MatchCollection matches = Regex.Matches(value, @"-?\d[\d,]*");
        if (matches.Count == 0)
        {
            return false;
        }

        long bestValue = 0;
        int bestDigitCount = -1;
        bool found = false;

        foreach (Match match in matches)
        {
            string raw = match.Value;
            string candidate = raw.Replace(",", "");
            if (!long.TryParse(candidate, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number))
            {
                continue;
            }

            string digitsOnly = candidate.TrimStart('-');
            int digitCount = digitsOnly.Length;
            if (digitCount > bestDigitCount || (digitCount == bestDigitCount))
            {
                bestDigitCount = digitCount;
                bestValue = number;
                found = true;
            }
        }

        if (!found)
        {
            return false;
        }

        parsed = bestValue;
        return true;
    }

    static void EnsureCharacterResultsSchema(SqliteConnection connection)
    {
        if (!TableExists(connection, "CHARACTER_RESULTS"))
        {
            using var createCommand = connection.CreateCommand();
            createCommand.CommandText =
                """
                CREATE TABLE CHARACTER_RESULTS (
                    nickname TEXT PRIMARY KEY,
                    job TEXT,
                    power TEXT,
                    atul_score TEXT,
                    updated_at TEXT NOT NULL
                );
                """;
            createCommand.ExecuteNonQuery();
            return;
        }

        bool hasUpdatedAt = ColumnExists(connection, "CHARACTER_RESULTS", "updated_at");
        bool nicknameIsPrimaryKey = IsPrimaryKeyColumn(connection, "CHARACTER_RESULTS", "nickname");

        if (hasUpdatedAt && nicknameIsPrimaryKey)
        {
            return;
        }

        using var tx = connection.BeginTransaction();

        using (var createNewCommand = connection.CreateCommand())
        {
            createNewCommand.Transaction = tx;
            createNewCommand.CommandText =
                """
                CREATE TABLE CHARACTER_RESULTS_NEW (
                    nickname TEXT PRIMARY KEY,
                    job TEXT,
                    power TEXT,
                    atul_score TEXT,
                    updated_at TEXT NOT NULL
                );
                """;
            createNewCommand.ExecuteNonQuery();
        }

        using (var copyCommand = connection.CreateCommand())
        {
            copyCommand.Transaction = tx;
            string updatedAtValue = GetKstNowString();
            string updatedAtSelect = hasUpdatedAt ? "COALESCE(updated_at, $updated_at)" : "$updated_at";
            copyCommand.CommandText =
                $"""
                INSERT INTO CHARACTER_RESULTS_NEW (nickname, job, power, atul_score, updated_at)
                SELECT nickname, job, power, atul_score, {updatedAtSelect}
                FROM CHARACTER_RESULTS
                WHERE nickname IS NOT NULL
                  AND TRIM(nickname) <> ''
                  AND rowid IN (
                    SELECT MAX(rowid)
                    FROM CHARACTER_RESULTS
                    WHERE nickname IS NOT NULL AND TRIM(nickname) <> ''
                    GROUP BY nickname
                  );
                """;
            copyCommand.Parameters.AddWithValue("$updated_at", updatedAtValue);
            copyCommand.ExecuteNonQuery();
        }

        using (var dropCommand = connection.CreateCommand())
        {
            dropCommand.Transaction = tx;
            dropCommand.CommandText = "DROP TABLE CHARACTER_RESULTS;";
            dropCommand.ExecuteNonQuery();
        }

        using (var renameCommand = connection.CreateCommand())
        {
            renameCommand.Transaction = tx;
            renameCommand.CommandText = "ALTER TABLE CHARACTER_RESULTS_NEW RENAME TO CHARACTER_RESULTS;";
            renameCommand.ExecuteNonQuery();
        }

        tx.Commit();
    }

    static void EnsureJobCountsSchema(SqliteConnection connection)
    {
        if (!TableExists(connection, "JOB_COUNTS"))
        {
            using var createCommand = connection.CreateCommand();
            createCommand.CommandText =
                """
                CREATE TABLE JOB_COUNTS (
                    job TEXT PRIMARY KEY,
                    job_count INTEGER NOT NULL,
                    updated_at TEXT NOT NULL
                );
                """;
            createCommand.ExecuteNonQuery();
            return;
        }

        if (ColumnExists(connection, "JOB_COUNTS", "updated_at"))
        {
            return;
        }

        using var tx = connection.BeginTransaction();

        using (var createNewCommand = connection.CreateCommand())
        {
            createNewCommand.Transaction = tx;
            createNewCommand.CommandText =
                """
                CREATE TABLE JOB_COUNTS_NEW (
                    job TEXT PRIMARY KEY,
                    job_count INTEGER NOT NULL,
                    updated_at TEXT NOT NULL
                );
                """;
            createNewCommand.ExecuteNonQuery();
        }

        using (var copyCommand = connection.CreateCommand())
        {
            copyCommand.Transaction = tx;
            copyCommand.CommandText =
                """
                INSERT INTO JOB_COUNTS_NEW (job, job_count, updated_at)
                SELECT job, job_count, $updated_at
                FROM JOB_COUNTS;
                """;
            copyCommand.Parameters.AddWithValue("$updated_at", GetKstNowString());
            copyCommand.ExecuteNonQuery();
        }

        using (var dropCommand = connection.CreateCommand())
        {
            dropCommand.Transaction = tx;
            dropCommand.CommandText = "DROP TABLE JOB_COUNTS;";
            dropCommand.ExecuteNonQuery();
        }

        using (var renameCommand = connection.CreateCommand())
        {
            renameCommand.Transaction = tx;
            renameCommand.CommandText = "ALTER TABLE JOB_COUNTS_NEW RENAME TO JOB_COUNTS;";
            renameCommand.ExecuteNonQuery();
        }

        tx.Commit();
    }

    static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name = $table_name;
            """;
        command.Parameters.AddWithValue("$table_name", tableName);
        long count = (long)(command.ExecuteScalar() ?? 0L);
        return count > 0;
    }

    static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            string currentColumn = reader.GetString(1);
            if (string.Equals(currentColumn, columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    static bool IsPrimaryKeyColumn(SqliteConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            string currentColumn = reader.GetString(1);
            int pk = reader.GetInt32(5);
            if (string.Equals(currentColumn, columnName, StringComparison.OrdinalIgnoreCase) && pk > 0)
            {
                return true;
            }
        }

        return false;
    }

    static void RefreshJobCounts(SqliteConnection connection)
    {
        using var tx = connection.BeginTransaction();
        string updatedAt = GetKstNowString();

        using (var clearCommand = connection.CreateCommand())
        {
            clearCommand.Transaction = tx;
            clearCommand.CommandText = "DELETE FROM JOB_COUNTS;";
            clearCommand.ExecuteNonQuery();
        }

        using (var aggregateCommand = connection.CreateCommand())
        {
            aggregateCommand.Transaction = tx;
            aggregateCommand.CommandText =
                """
                INSERT INTO JOB_COUNTS (job, job_count, updated_at)
                SELECT job, COUNT(*), $updated_at
                FROM CHARACTER_RESULTS
                WHERE job IS NOT NULL AND TRIM(job) <> ''
                GROUP BY job;
                """;
            aggregateCommand.Parameters.AddWithValue("$updated_at", updatedAt);
            aggregateCommand.ExecuteNonQuery();
        }

        using (var totalCommand = connection.CreateCommand())
        {
            totalCommand.Transaction = tx;
            totalCommand.CommandText =
                """
                INSERT INTO JOB_COUNTS (job, job_count, updated_at)
                SELECT 'TOTAL', COUNT(*), $updated_at
                FROM CHARACTER_RESULTS;
                """;
            totalCommand.Parameters.AddWithValue("$updated_at", updatedAt);
            totalCommand.ExecuteNonQuery();
        }

        tx.Commit();
    }

    static List<CharacterTarget> ReadTargets(string path, int defaultServerId)
    {
        var targets = new List<CharacterTarget>();

        foreach (string rawLine in ReadLinesWithFallback(path))
        {
            if (TryParseTargetLine(rawLine, defaultServerId, out CharacterTarget target))
            {
                targets.Add(target);
            }
        }

        return targets;
    }

    static bool TryParseTargetLine(string rawLine, int defaultServerId, out CharacterTarget target)
    {
        target = new CharacterTarget(defaultServerId, "");
        string line = RemoveParenthesizedSections(rawLine).Trim();
        if (line.Length == 0 || line.StartsWith("#"))
        {
            return false;
        }

        int serverId = defaultServerId;
        string name = line;

        string[] parts = line.Split(',', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0)
        {
            if (!int.TryParse(parts[0], out int parsedServerId))
            {
                Console.WriteLine($"잘못된 서버 ID 형식이라 건너뜀: {line}");
                return false;
            }

            serverId = parsedServerId;
            name = parts[1];
        }

        target = new CharacterTarget(serverId, name);
        return true;
    }

    static void UpsertTargetInListByJob(
        string listPath,
        string nickname,
        int serverId,
        string job,
        string dbPath)
    {
        if (!File.Exists(listPath))
        {
            File.WriteAllText(listPath, "", new UTF8Encoding(true));
        }

        var originalLines = ReadLinesWithFallback(listPath).ToList();
        var nicknameJobMap = LoadNicknameJobMapFromDb(dbPath);
        string normalizedNewJob = NormalizeJobName(job);
        string newLine = serverId == FallbackDefaultServerId ? nickname : $"{serverId}, {nickname}";

        for (int i = 0; i < originalLines.Count; i++)
        {
            string rawLine = originalLines[i];
            if (!TryParseTargetLine(rawLine, FallbackDefaultServerId, out CharacterTarget parsed))
            {
                continue;
            }

            if (string.Equals(parsed.Name, nickname, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"list.txt에 이미 존재합니다: {parsed.Name} (ServerId={parsed.ServerId})");
                return;
            }
        }

        int insertIndex = originalLines.Count;
        for (int i = 0; i < originalLines.Count; i++)
        {
            string rawLine = originalLines[i];
            if (!TryParseTargetLine(rawLine, FallbackDefaultServerId, out CharacterTarget parsed))
            {
                continue;
            }

            string existingJob = nicknameJobMap.TryGetValue(parsed.Name, out string mappedJob)
                ? mappedJob
                : "-";

            int cmp = CompareJobThenName(
                normalizedNewJob,
                nickname,
                NormalizeJobName(existingJob),
                parsed.Name);

            if (cmp < 0)
            {
                insertIndex = i;
                break;
            }
        }

        originalLines.Insert(insertIndex, newLine);
        File.WriteAllLines(listPath, originalLines, new UTF8Encoding(true));
        Console.WriteLine($"list.txt 추가 완료: {nickname} (정렬 위치 {insertIndex + 1})");
    }

    static Dictionary<string, string> LoadNicknameJobMapFromDb(string dbPath)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(dbPath))
        {
            return map;
        }

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        if (!TableExists(connection, "CHARACTER_RESULTS"))
        {
            return map;
        }

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT nickname, job
            FROM CHARACTER_RESULTS;
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            string name = reader.IsDBNull(0) ? "" : reader.GetString(0);
            string job = reader.IsDBNull(1) ? "-" : reader.GetString(1);
            if (name.Length == 0)
            {
                continue;
            }

            map[name] = NormalizeJobName(job);
        }

        return map;
    }

    static int CompareJobThenName(string leftJob, string leftName, string rightJob, string rightName)
    {
        int leftOrder = GetJobSortIndex(leftJob);
        int rightOrder = GetJobSortIndex(rightJob);
        if (leftOrder != rightOrder)
        {
            return leftOrder.CompareTo(rightOrder);
        }

        int jobCompare = string.Compare(leftJob, rightJob, StringComparison.Ordinal);
        if (jobCompare != 0)
        {
            return jobCompare;
        }

        return string.Compare(leftName, rightName, StringComparison.Ordinal);
    }

    static void UpdateLatestExcelForUpsert(
        string nickname,
        string job,
        string power,
        string score,
        string dbPath)
    {
        string latestPath = GetLatestExcelPath() ?? BuildTimestampedExcelPath();
        List<ComparisonRow> rows = File.Exists(latestPath)
            ? ReadComparisonRowsFromExcel(latestPath)
            : new List<ComparisonRow>();

        rows.RemoveAll(x => string.Equals(x.Nickname, nickname, StringComparison.OrdinalIgnoreCase));
        rows.Add(new ComparisonRow(
            nickname,
            NormalizeJobName(job),
            power,
            score,
            power,
            score,
            "0",
            "0"));

        Dictionary<string, int> totals = LoadJobTotalsFromDbPath(dbPath);
        string writtenPath = WriteComparisonExcel(latestPath, rows, totals);
        Console.WriteLine($"엑셀 갱신 완료: {writtenPath}");
    }

    static void UpdateLatestExcelForRemove(string nickname, string dbPath)
    {
        string? latestPath = GetLatestExcelPath();
        if (latestPath is null || !File.Exists(latestPath))
        {
            return;
        }

        List<ComparisonRow> rows = ReadComparisonRowsFromExcel(latestPath);
        int removedCount = rows.RemoveAll(x => string.Equals(x.Nickname, nickname, StringComparison.OrdinalIgnoreCase));
        if (removedCount == 0)
        {
            return;
        }

        Dictionary<string, int> totals = LoadJobTotalsFromDbPath(dbPath);
        string writtenPath = WriteComparisonExcel(latestPath, rows, totals);
        Console.WriteLine($"엑셀 삭제 반영 완료: {writtenPath}");
    }

    static Dictionary<string, int> LoadJobTotalsFromDbPath(string dbPath)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!File.Exists(dbPath))
        {
            return map;
        }

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        if (!TableExists(connection, "JOB_COUNTS"))
        {
            return map;
        }

        return LoadJobTotals(connection);
    }

    static string? GetLatestExcelPath()
    {
        string resultDir = Path.Combine(AppContext.BaseDirectory, ResultFolderName);
        if (!Directory.Exists(resultDir))
        {
            return null;
        }

        return Directory
            .GetFiles(resultDir, $"{CsvFilePrefix}_*.xlsx")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    static List<ComparisonRow> ReadComparisonRowsFromExcel(string excelPath)
    {
        var rows = new List<ComparisonRow>();
        if (!File.Exists(excelPath))
        {
            return rows;
        }

        using var workbook = new XLWorkbook(excelPath);
        IXLWorksheet? sheet = workbook.Worksheets.FirstOrDefault();
        if (sheet is null)
        {
            return rows;
        }

        int lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (int rowIndex = 2; rowIndex <= lastRow; rowIndex++)
        {
            string nickname = sheet.Cell(rowIndex, 3).GetValue<string>().Trim();
            if (nickname.Length == 0)
            {
                continue;
            }

            string job = sheet.Cell(rowIndex, 4).GetValue<string>();
            string level = sheet.Cell(rowIndex, 5).GetValue<string>();
            string atul = sheet.Cell(rowIndex, 6).GetValue<string>();
            string levelCompare = sheet.Cell(rowIndex, 7).GetValue<string>();
            string atulCompare = sheet.Cell(rowIndex, 8).GetValue<string>();
            string levelDelta = sheet.Cell(rowIndex, 9).GetValue<string>();
            string atulDelta = sheet.Cell(rowIndex, 10).GetValue<string>();

            if (level.Length == 0) level = "0";
            if (atul.Length == 0) atul = "0";
            if (levelCompare.Length == 0) levelCompare = level;
            if (atulCompare.Length == 0) atulCompare = atul;
            if (levelDelta.Length == 0) levelDelta = "0";
            if (atulDelta.Length == 0) atulDelta = "0";

            rows.Add(new ComparisonRow(
                nickname,
                NormalizeJobName(job),
                level,
                atul,
                levelCompare,
                atulCompare,
                levelDelta,
                atulDelta));
        }

        return rows;
    }

    static bool RemoveTargetFromList(string listPath, string nickname)
    {
        if (!File.Exists(listPath))
        {
            return false;
        }

        var originalLines = ReadLinesWithFallback(listPath).ToList();
        var keptLines = new List<string>();
        bool removed = false;

        foreach (string rawLine in originalLines)
        {
            if (!TryParseTargetLine(rawLine, FallbackDefaultServerId, out CharacterTarget parsed))
            {
                keptLines.Add(rawLine);
                continue;
            }

            if (string.Equals(parsed.Name, nickname, StringComparison.OrdinalIgnoreCase))
            {
                removed = true;
                continue;
            }

            keptLines.Add(rawLine);
        }

        if (removed)
        {
            File.WriteAllLines(listPath, keptLines, new UTF8Encoding(true));
        }

        return removed;
    }

    static bool RemoveTargetFromDb(string dbPath, string nickname)
    {
        if (!File.Exists(dbPath))
        {
            return false;
        }

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        if (!TableExists(connection, "CHARACTER_RESULTS"))
        {
            return false;
        }

        EnsureCharacterResultsSchema(connection);
        EnsureJobCountsSchema(connection);

        int deleted;
        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.CommandText =
                """
                DELETE FROM CHARACTER_RESULTS
                WHERE nickname = $nickname COLLATE NOCASE;
                """;
            deleteCommand.Parameters.AddWithValue("$nickname", nickname);
            deleted = deleteCommand.ExecuteNonQuery();
        }

        RefreshJobCounts(connection);
        return deleted > 0;
    }

    static string RemoveParenthesizedSections(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return Regex.Replace(input, @"\([^)]*\)", "").Trim();
    }

    static IEnumerable<string> ReadLinesWithFallback(string path)
    {
        string[] utf8Lines = File.ReadAllLines(path, Encoding.UTF8);
        bool hasReplacementChar = false;
        foreach (string line in utf8Lines)
        {
            if (line.Contains('\uFFFD'))
            {
                hasReplacementChar = true;
                break;
            }
        }

        if (!hasReplacementChar)
        {
            return utf8Lines;
        }

        return File.ReadAllLines(path, Encoding.Default);
    }

    static async Task<string> GetText(IPage page, string selector)
    {
        try
        {
            var locator = page.Locator(selector);
            if (await locator.CountAsync() == 0)
            {
                return "";
            }

            string? text = await locator.First.TextContentAsync();
            return text?.Trim() ?? "";
        }
        catch
        {
            return "";
        }
    }

    static string GetKstNowString()
    {
        DateTime kstNow = TimeZoneInfo.ConvertTime(DateTime.UtcNow, KstTimeZone);
        return kstNow.ToString("yyyy-MM-dd HH:mm:ss");
    }

    static TimeZoneInfo ResolveKstTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
        }
        catch
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
        }
    }

    private sealed record CharacterTarget(int ServerId, string Name);
    private sealed record CharacterSnapshot(string Job, string Power, string AtulScore);
    private sealed record ComparisonRow(
        string Nickname,
        string Job,
        string Level,
        string Atul,
        string LevelCompare,
        string AtulCompare,
        string LevelDelta,
        string AtulDelta);
    private sealed record CharacterResult(
        int ServerId,
        string Name,
        string Nickname,
        string Job,
        string Power,
        string Score,
        string Error);

    static async Task<CharacterResult> FetchCharacterAsync(IBrowserContext context, CharacterTarget target)
    {
        string url = $"https://aion2tool.com/char/serverid={target.ServerId}/{Uri.EscapeDataString(target.Name)}";
        IPage? page = null;

        try
        {
            page = await context.NewPageAsync();
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 60000
            });

            await page.WaitForSelectorAsync("#result-nickname", new PageWaitForSelectorOptions
            {
                Timeout = 60000
            });

            string nickname = await GetText(page, "#result-nickname");
            string job = await GetText(page, "#result-job");
            string power = await GetText(page, "#result-combat-power");
            string score = await GetText(page, "#dps-score-value");

            return new CharacterResult(target.ServerId, target.Name, nickname, job, power, score, "");
        }
        catch (Exception ex)
        {
            return new CharacterResult(target.ServerId, target.Name, "", "", "", "", ex.Message);
        }
        finally
        {
            if (page is not null)
            {
                await page.CloseAsync();
            }
        }
    }

    private sealed record AppSettings(int DefaultServerId, int MaxConcurrency);
}
