/*
 * Crawling is intentionally disabled for AION_Party.1.0.
 *
 * The old AION_ver.2.0 crawler used Playwright to fetch character job/power
 * from a web page. This party project currently trusts list.txt as the source
 * of truth, so no browser, database, or network code is executed.
 *
 * Keep this placeholder so crawling can be restored later without changing the
 * party optimizer flow:
 *
 * static async Task<CharacterInfo> FetchCharacterAsync(string name)
 * {
 *     using var playwright = await Playwright.CreateAsync();
 *     await using var browser = await playwright.Chromium.LaunchAsync(
 *         new BrowserTypeLaunchOptions { Headless = true });
 *
 *     IPage page = await browser.NewPageAsync();
 *     await page.GotoAsync($"https://aion2tool.com/char/serverid=1010/{Uri.EscapeDataString(name)}");
 *
 *     string job = await page.Locator("#result-job").InnerTextAsync();
 *     string power = await page.Locator("#result-combat-power").InnerTextAsync();
 *
 *     return new CharacterInfo(name, job, power);
 * }
 */
