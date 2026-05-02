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

partial class Program
{
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

            string existingJob = nicknameJobMap.TryGetValue(parsed.Name, out string? mappedJob)
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

    static Dictionary<string, CharacterSnapshot> LoadCharacterSnapshotsFromExcel(string excelPath)
    {
        var map = new Dictionary<string, CharacterSnapshot>(StringComparer.Ordinal);

        foreach (ComparisonRow row in ReadComparisonRowsFromExcel(excelPath))
        {
            if (string.IsNullOrWhiteSpace(row.Nickname))
            {
                continue;
            }

            string job = string.IsNullOrWhiteSpace(row.Job) ? "-" : NormalizeJobName(row.Job);
            string power = string.IsNullOrWhiteSpace(row.LevelCompare) ? row.Level : row.LevelCompare;
            string atulScore = string.IsNullOrWhiteSpace(row.AtulCompare) ? row.Atul : row.AtulCompare;
            map[row.Nickname] = new CharacterSnapshot(job, power, atulScore);
        }

        return map;
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
}
