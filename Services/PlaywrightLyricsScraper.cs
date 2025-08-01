using Microsoft.Playwright;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Alify.Services
{
    public class PlaywrightLyricsScraper
    {
        public async Task<string?> ScrapeLyricsAsync(string songUrl)
        {
            using IPlaywright playwright = await Playwright.CreateAsync();
            IBrowser browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            IPage page = await browser.NewPageAsync();

            await page.GotoAsync(songUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await page.WaitForSelectorAsync("div[class*='Lyrics__Container']");

            IReadOnlyList<IElementHandle> lyricsBlocks = await page.QuerySelectorAllAsync("div[class*='Lyrics__Container']");
            List<string> lyrics = new List<string>();

            foreach (var block in lyricsBlocks)
            {
                string text = await block.InnerTextAsync();
                lyrics.Add(text.Trim());
            }

            await browser.CloseAsync();
            return lyrics.Count > 0 ? string.Join("\n", lyrics) : null;
        }
    }
}
