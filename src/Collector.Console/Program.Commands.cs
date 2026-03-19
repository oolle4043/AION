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
            Console.WriteLine($"파괴 리스트 점수: {result.Score}");
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
}
