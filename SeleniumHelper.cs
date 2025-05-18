using OpenQA.Selenium.Chrome;

namespace DownloadBooktoki
{
    public class SeleniumHelper
    {
        public static ChromeDriver? driver;

        public static async Task<string> GetHtml(string url)
        {
            if (driver == null)
            {
                await InstallChromeDriver.InstallAsync();

                //string dir = "C:\\Users\\mrsio\\AppData\\Local\\Google\\Chrome\\User Data";
                //dir = Path.Combine(Directory.GetCurrentDirectory(), "UserData");
                //string name = "Default";
                //name = "Profile 2";

                ChromeDriverService service = ChromeDriverService.CreateDefaultService();
                service.EnableVerboseLogging = false;  // 자세한 로그 비활성화
                service.SuppressInitialDiagnosticInformation = true;  // 초기 진단 정보 출력 억제
                service.HideCommandPromptWindow = true;  // 명령 프롬프트 창 숨기기

                ChromeOptions options = new ChromeOptions();
                options.AddArgument(@"--user-data-dir=C:\Users\mrsio\AppData\Local\Google\Chrome\User Data");
                options.AddArgument("--profile-directory=Default");
                //options.AddArgument("--no-sandbox");
                //options.AddArgument("--remote-debugging-port=0");
                //options.AddArgument("--disable-dev-shm-usage");
                //options.AddArgument("--disable-extensions");
                //options.AddArgument("--disable-blink-features=AutomationControlled");

                options.AddExcludedArgument("enable-automation");
                //options.AddAdditionalOption("useAutomationExtension", false);

                //options.AddArgument("--disable-blink-features=AutomationControlled"); // Selenium 탐지 우회
                //options.AddExcludedArgument("enable-automation"); // "Chrome is being controlled" 메시지 숨기기
                //options.AddArgument("--no-sandbox"); // 샌드박스 비활성화
                //options.AddArgument("--disable-infobars"); // 정보 바 숨기기
                //options.AddArgument("--disable-dev-shm-usage"); // 메모리 사용 비활성화

                //string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36";
                //options.AddArgument($"--user-agent={userAgent}");

                driver = new ChromeDriver(service, options);

                // CDP 명령어로 탐지 우회 (Chrome DevTools Protocol 사용)
                //var devTools = ((ChromeDriver)driver).ExecuteCdpCommand("Page.addScriptToEvaluateOnNewDocument", new Dictionary<string, object>
                //{
                //    {"source", @"
                //        Object.defineProperty(navigator, 'webdriver', {
                //            get: () => undefined
                //        });
                //    "}
                //});

                AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
            }

            await driver.Navigate().GoToUrlAsync(url);

            string html = driver.PageSource;
            while (!App.IsCandidate(html))
            {
                if(driver.HasActiveDevToolsSession)
                {
                    driver.GetDevToolsSession().Dispose();
                    Console.ReadLine();
                }

                html = driver.PageSource;
                await Task.Delay(1000);
            }

            return html;
        }

        private static void OnProcessExit(object? sender, EventArgs e)
        {
            driver?.Quit();
            driver = null;
        }
    }
}
