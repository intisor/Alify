using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Alify.Services
{
    public class PlaywrightLyricsScraper
    {
        private readonly ILogger<PlaywrightLyricsScraper> _logger;

        public PlaywrightLyricsScraper(ILogger<PlaywrightLyricsScraper> logger)
        {
            _logger = logger;
        }

        public async Task<string?> ScrapeLyricsAsync(string songUrl)
        {
            using IPlaywright playwright = await Playwright.CreateAsync();
            IBrowser browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[]
                {
                        "--disable-gpu",
                        "--no-sandbox",
                        "--disable-dev-shm-usage",
                        "--disable-extensions",
                        "--disable-background-networking",
                        "--disable-sync",
                        "--disable-default-apps",
                        "--disable-translate",
                        "--disable-background-timer-throttling",
                        "--disable-renderer-backgrounding",
                        "--disable-device-discovery-notifications",
                        "--mute-audio",
                        "--blink-settings=imagesEnabled=false",
                        "--disable-image-loading",
                        "--disable-javascript-harmony-shipping",
                        "--js-flags=--expose-gc",
                        "--disable-threaded-animation",
                        "--disable-threaded-scrolling",
                        "--disable-composited-antialiasing"
                    }
            });
            IBrowserContext context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 800, Height = 600 },
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                JavaScriptEnabled = true,
                BypassCSP = true
            });
            IPage page = await context.NewPageAsync();

            try
            {
                // Block image requests
                await page.RouteAsync("**/*.{png,jpg,jpeg,gif,webp,svg}", route => route.AbortAsync());

                // Block other unnecessary resources
                await page.RouteAsync("**/*.{css,woff,woff2,ttf,otf}", route => route.AbortAsync());

                await page.GotoAsync(songUrl, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 30000
                });
                await page.WaitForSelectorAsync("div[class*='Lyrics__Container']", new PageWaitForSelectorOptions { Timeout = 60000 });

                IReadOnlyList<IElementHandle> lyricsBlocks = await page.QuerySelectorAllAsync("div[class*='Lyrics__Container']");
                List<string> lyrics = [];

                foreach (var block in lyricsBlocks)
                {
                    string text = await block.InnerTextAsync();
                    lyrics.Add(text.Trim());
                }

                await browser.CloseAsync();
                return lyrics.Count > 0 ? string.Join("\n", lyrics) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping lyrics for {SongUrl}", songUrl);
                return null;
            }
        }
    }
}
