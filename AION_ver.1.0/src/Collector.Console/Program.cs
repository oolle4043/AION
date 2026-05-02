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
                Console.WriteLine($"파괴 리스트 점수: {result.Score}");
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
}

