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
