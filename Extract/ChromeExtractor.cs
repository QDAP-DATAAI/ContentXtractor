using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PuppeteerSharp;

namespace ContentXtractor.Extract
{
    public static partial class ChromeExtractor
    {
        private const int DefaultReadingModeDistillationTimeout = 15 * 1000; //timeout in milliseconds to wait for reading mode distillation.
        private const string ReadingModeUrl = "chrome-untrusted://read-anything-side-panel.top-chrome/";
        private const string PdfContentType = "application/pdf";
        private static readonly string[] AllowedContentType = ["text/html", "text/plain", PdfContentType];

        public static async Task<ExtractResult> Extract(string url, bool headless = true, ViewPortOptions? viewPortOptions = default, bool disableLinks = true, bool returnRawHtml = false, WaitUntilNavigation? waitUntil = default, int? readingModeTimeout = default, ILoggerFactory? loggerFactory = default, CancellationToken cancellationToken = default)
        {
            var options = MakeLaunchOptions(headless);

            try
            {
                using var browser = await Puppeteer.LaunchAsync(options, loggerFactory);
                using var _ = cancellationToken.Register(browser.Dispose);

                //disable downloads
                var session = await browser.Target.CreateCDPSessionAsync();
                await session.SendAsync("Browser.setDownloadBehavior", new
                {
                    behavior = "deny",
                    eventsEnabled = true
                });

                InstallScreenAi(options.UserDataDir);

                var pages = await browser.DefaultContext.PagesAsync();
                //this should be the tab loaded with "data:text/plain," from MakeLaunchOptions.
                var page = pages.Length == 1 ? pages[0] : throw new Exception($"There should be 1 page available but found {pages.Length}.");

                //remove "Headless" from user agent so websites don't block headless mode.
                var currentUserAgent = await browser.GetUserAgentAsync();
                await page.SetUserAgentAsync(currentUserAgent.Replace("Headless", ""));

                //configure viewport
                await page.SetViewportAsync(viewPortOptions ?? ViewPortOptions.Default);

                //this is how navigation to reading mode should be done because it returns net::ERR_ABORTED, which is expected, but puppeteer will throw an exception if GoToAsync is used.
                await page.Client.SendAsync("Page.navigate", new
                {
                    Url = ReadingModeUrl,
                });

                var readingModeTargets = browser.Targets().Where(target => target.Url == ReadingModeUrl).ToArray();
                var readingModePage = readingModeTargets.Length == 1 ? await readingModeTargets[0].AsPageAsync() : throw new Exception($"There should be 1 target for reading mode available but found {readingModeTargets.Length}.");

                if (disableLinks)
                {
                    await readingModePage.WaitForSelectorAsync("read-anything-toolbar");
                    await readingModePage.EvaluateExpressionAsync("document.querySelector('read-anything-toolbar').shadowRoot.querySelector('#link-toggle-button').click()");
                }

                var requestFinishedTask = MakeNextRequestFinishedInterceptTask(page);
                var goToResult = await page.GoToAsync(url, waitUntil ?? WaitUntilNavigation.Load);

                var requestFinishedResult = await requestFinishedTask;
                if (requestFinishedResult.StatusCode != HttpStatusCode.OK || !IsContentTypeAllowed(requestFinishedResult.ContentType))
                    return new ExtractResult(false, returnRawHtml ? await page.GetContentAsync() : null, "", false, [], requestFinishedResult);

                if (IsPdf(requestFinishedResult.ContentType))
                {
                    //some times pdfs are not loaded correctly in reading mode, but if you change tabs it does, so this hack does that.
                    await browser.NewPageAsync();
                    await Task.Delay(2000, cancellationToken);
                    await page.BringToFrontAsync();
                }

                var urls = await page.EvaluateExpressionAsync<string[]>("Array.from(document.querySelectorAll('a')).map((a) => a.href).filter((href) => href)");

                var readingModeReady = await WaitForReadingMode(readingModePage, readingModeTimeout ?? DefaultReadingModeDistillationTimeout);
                var pageToExtract = readingModeReady ? readingModePage : page;

                var turndownJs = File.ReadAllText(Path.Combine("Extract", "assets", "extensions", "turndown.js"));
                await pageToExtract.EvaluateExpressionAsync(turndownJs);

                var markdown = await pageToExtract.EvaluateExpressionAsync<string>("""
                    const turndownService = new TurndownService();

                    turndownService.remove(["link", "script", "style", "noscript", "object", "embed", "iron-iconset-svg", "cr-toast"]);
                    turndownService.turndown(document.body).trim();
                """);

                return new ExtractResult(true, returnRawHtml ? await page.GetContentAsync() : null, markdown, readingModeReady, urls, requestFinishedResult);
            }
            catch (PuppeteerException) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException();
            }
            finally
            {
                ScheduleUserDataDirDeletion(options.UserDataDir);
            }
        }

        public record ExtractResult(bool Success, string? RawHtml, string Markdown, bool ExtractedFromReadingMode, string[] Urls, RequestFinishedResult RequestResult);

        private static LaunchOptions MakeLaunchOptions(bool headless)
        {
            return new LaunchOptions
            {
                Headless = headless,
                ExecutablePath = ChromePath,
                UserDataDir = Directory.CreateTempSubdirectory("chrome_user_data_dir_").FullName,
                Args = [
                    "--disable-web-security",
                    "--remote-allow-origins=*",
                    "--disable-gpu",
                    "--no-sandbox", //this is needed for docker
                    "--enable-features=ReadAnything,ReadAnythingWithScreen2x,ReadAnythingWebUIToolbar", //enable features needed for reading mode.
                    "--disable-features=ReadAnythingLocalSidePanel", //this disables the local (per tab) reading mode side panel so there is only one side panel for reading mode instead of one for each tab.
                    "data:text/plain," //initial "url" to load; this is a blank page so when reading mode loads, it loads ready.
                ],
            };
        }

        private static string? ChromePath
        {
            get
            {
                if (OperatingSystem.IsOSPlatform("windows"))
                {
                    var registryKeys = new[]
                    {
                        @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe",
                        @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe"
                    };

                    foreach (var key in registryKeys)
                    {
                        var path = (string?)Registry.GetValue(key, "", null);
                        if (!string.IsNullOrEmpty(path))
                        {
                            return path;
                        }
                    }

                    return null;
                }
                else
                {
                    return "google-chrome";
                }
            }
        }

        private static void InstallScreenAi(string userDataDir)
        {
            const string ScreenAiPath = "screen_ai";

            var platform = "win64"; //only windows 64bit; windows is only used for development and I don't think you're using a x86 machine =)
            if (OperatingSystem.IsOSPlatform("linux"))
                platform = "linux";
            else if (!OperatingSystem.IsOSPlatform("windows"))
                throw new PlatformNotSupportedException("Only windows and linux are supported.");

            var path = Path.Combine("Extract", "assets", "extensions", ScreenAiPath);
            var file = Directory.EnumerateFiles(path, $"mfhmdacoffpmifoibamicehhklffanao_*_{platform}_*.crx3").OrderByDescending(x => x).FirstOrDefault() ?? throw new FileNotFoundException("ScreenAi CRX3 file not found.");

            var match = ScreenAiVersionRegex().Match(Path.GetFileName(file));
            if (match == null || !match.Success)
                throw new Exception("Invalid CRX3 file name.");

            var version = match.Groups[1].Value;

            using var crx3 = new Crx3FileZipReaderStream(file);
            ZipFile.ExtractToDirectory(crx3, Path.Combine(userDataDir, ScreenAiPath, version), true);
        }

        private static async Task<bool> WaitForReadingMode(IPage page, int timeout)
        {
            try
            {
                //this is how to wait for the reading mode page to load, when <div id="container-parent"> is visible.
                await page.WaitForSelectorAsync("#container-parent", new WaitForSelectorOptions { Visible = true, Timeout = timeout });
                return true;
            }
            catch (WaitTaskTimeoutException)
            {
                return false;
            }
        }

        private static Task<RequestFinishedResult> MakeNextRequestFinishedInterceptTask(IPage page)
        {
            var resultTcs = new TaskCompletionSource<RequestFinishedResult>();
            var urlTcs = new TaskCompletionSource<string>();

            page.Request += request;
            page.RequestFinished += requestFinished;

            return resultTcs.Task;

            void request(object? sender, RequestEventArgs e)
            {
                urlTcs.SetResult(e.Request.Url);
                page.Request -= request;
            }

            async void requestFinished(object? sender, RequestEventArgs e)
            {
                var url = await urlTcs.Task;
                if (e.Request.Url != url)
                    return;

                e.Request.Response.Headers.TryGetValue("Content-Type", out var contentType);
                if (e.Request.Response.Status != HttpStatusCode.OK && e.Request.Response.Headers.TryGetValue("Location", out var location))
                {
                    url = location;
                }

                resultTcs.SetResult(new RequestFinishedResult(url, contentType ?? "", e.Request.RedirectChain.Length != 0, e.Request.Response.Status));
                page.RequestFinished -= requestFinished;
            }
        }

        private static bool IsContentTypeAllowed(string contentType) => AllowedContentType.Any(type => contentType.StartsWith(type, StringComparison.OrdinalIgnoreCase));

        private static bool IsPdf(string contentType) => contentType.StartsWith(PdfContentType, StringComparison.OrdinalIgnoreCase);

        private static void ScheduleUserDataDirDeletion(string userDataDir)
        {
            Task.Run(async () =>
            {
                while (Directory.Exists(userDataDir))
                {
                    await Task.Delay(1000);

                    try
                    {
                        Directory.Delete(userDataDir, true);
                    }
                    catch (Exception)
                    {
                    }
                }
            }).Forget();
        }

        [GeneratedRegex("mfhmdacoffpmifoibamicehhklffanao_(.*?)_", RegexOptions.Compiled)]
        private static partial Regex ScreenAiVersionRegex();
    }

    public record RequestFinishedResult(string Url, string ContentType, bool Redirected, HttpStatusCode StatusCode);
}