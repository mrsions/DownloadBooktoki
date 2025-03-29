#define USE_SELENIUM

using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

using Fizzler.Systems.HtmlAgilityPack;

using HtmlAgilityPack;
using OpenQA.Selenium;

namespace DownloadBooktoki
{
    public record class ViewItem(string url, string name, string file)
    {
    }

    public class App
    {
        private const string MERGE_PATH = "Novels/Merges";
        public static int httpDelay = 1;

        public static async Task Start(string[] args)
        {
            await InstallChromeDriver.InstallAsync();

            string txt = await GetHtml($"https://t.me/s/newtoki5", useCache: false);

            int code = Regex.Matches(txt, @"https://booktoki(\d+)\.com")
                .Where(m => m.Success)
                .Select(m => int.Parse(m.Groups[1].Value))
                .OrderDescending()
                .FirstOrDefault();

            string url = $"https://booktoki{code}.com";
            //await GetHtml(url+"/novel", useCache:false);

            while (true)
            {
                Console.WriteLine("----------------------------");
                Console.WriteLine("1. 수정");
                Console.WriteLine("2. 시작");
                Console.WriteLine("3. 합치기");

                var selectText = Console.ReadLine();
                Console.WriteLine();
                try
                {
                    if (selectText != null && int.TryParse(selectText.Trim(), out int select))
                    {
                        switch (select)
                        {
                            case 1:
                                // 수정
                                break;

                            case 2:
                                await DownloadAsync(url);
                                break;

                            case 3:
                                await MergeAll();
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    Console.WriteLine("계속하려면 입력하세요.");
                    Console.ReadLine();
                }
                Console.Clear();
            }
        }

        private static async Task MergeAll()
        {
            const int SPLIT = 50;

            Directory.Delete(MERGE_PATH, true);

            var dirs = Directory.GetDirectories("Novels");
            Directory.CreateDirectory(MERGE_PATH);

            for (int i1 = 0; i1 < dirs.Length; i1++)
            {
                string? dir = dirs[i1];
                var name = Path.GetFileName(dir);
                Log($"{name} ({i1}/{dirs.Length})");

                var files = Directory.GetFiles(dir, "*.txt", SearchOption.TopDirectoryOnly)
                    .Where(v => int.TryParse(Regex.Match(v, @"\d+").Value, out _))
                    .OrderBy(v => int.Parse(Regex.Match(v, @"\d+").Value))
                    .ToArray();

                var dirPath = $"{MERGE_PATH}/{name}";
                Directory.CreateDirectory(dirPath);

                StreamWriter? writer = null;
                try
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        if (writer == null || i % SPLIT == 0)
                        {
                            writer?.Dispose();
                            int start = i + 1;
                            int end = Math.Min(i + SPLIT, files.Length);

                            string path = $"{dirPath}/{name}({start}~{end}).txt";
                            writer = new StreamWriter(File.OpenWrite(path), Encoding.UTF8);
                        }

                        writer.WriteLine(NormalizeText(File.ReadAllText(files[i])));
                        writer.WriteLine();
                        writer.WriteLine();
                        writer.WriteLine();
                    }
                }
                finally
                {
                    try { writer?.Dispose(); } catch { }
                }
            }

            Log("계속 진행하려면 Enter를 누르세요.");
            Console.ReadLine();
            Console.Clear();
        }

        private static async Task DownloadAsync(string root)
        {
            Console.Clear();

            httpDelay = int.Parse(File.ReadAllText("config/delay.txt"));
            string[] hrefs = File.ReadAllLines("config/href.txt")
                .Where(v => !string.IsNullOrWhiteSpace(v) && !v.StartsWith("#"))
                .Select(v => v.Split("\t")[0])
                .Distinct().ToArray();
            for (int i = 0; i < hrefs.Length; i++)
            {
                string? href = root + hrefs[i];

                var read = await ReadList(href);
                var novelName = read.Item1;
                var items = read.Item2;

                Log($"[{i}/{hrefs.Length}] '{novelName}' (총 {items.Count}개)");

                items.Reverse();
                for (int j = 0; j < items.Count; j++)
                {
                    ViewItem? item = items[j];

                    long remainTime = (items.Count - j) * (httpDelay + 1) * TimeSpan.TicksPerSecond;

                    Log($"[{i}/{hrefs.Length}] '{novelName}' {item.file} ({j}/{items.Count}, 남은시간: {new TimeSpan(remainTime)})");


                    await ReadNovel(root, item, novelName);
                }
            }

            Log("다운로드 완료.");
            Log("계속 진행하려면 Enter를 누르세요.");
            Console.ReadLine();
            Console.Clear();
        }

        private static void Log(string log)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {log}");
        }

        private static async Task<(string, List<ViewItem>)> ReadList(string listUrl)
        {
            List<ViewItem> items = new();

            var html = await GetHtml(listUrl, $"{DateTime.Now:yyyyMMdd}.list");

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);
            var articleNode = htmlDoc.DocumentNode.QuerySelector("article");

            var titleNode = articleNode.QuerySelector("div.view-title div.view-content > span > b");
            var novelName = titleNode.InnerText.Trim();

            var dir = $"Novels/{RenameArticle(novelName)}";
            Directory.CreateDirectory($"Novels/{novelName}");

            var listItems = articleNode.QuerySelectorAll("li.list-item");
            if (listItems != null)
            {
                foreach (var item in listItems)
                {
                    // 4. 링크(href)와 텍스트를 가져옵니다.
                    var anchor = item.SelectSingleNode(".//a");
                    if (anchor != null)
                    {
                        // 5. <a> 안에 있는 <span> 요소를 제거합니다.
                        var spanToRemove = anchor.SelectNodes(".//span");
                        if (spanToRemove != null)
                        {
                            foreach (var node in spanToRemove)
                            {
                                node.Remove();
                            }
                        }

                        var name = anchor.InnerText.Trim();
                        var numberMatch = Regex.Match(name, @"\s*(\d+)화");
                        if (numberMatch != null)
                        {
                            name = numberMatch.Groups[1].Value;
                        }
                        else if (name.Length > novelName.Length)
                        {
                            name = name.Substring(novelName.Length).Trim();
                        }

                        var href = new Uri(anchor.GetAttributeValue("href", string.Empty)).PathAndQuery;
                        href = href.Replace("&amp;", "&");
                        var file = $"{dir}/{RenameArticle(name)}.txt";

                        items.Add(new(href, name, file));
                    }
                }
            }

            return (novelName, items);
        }

        static Regex selectSeparate1 = new Regex(@"^\s*(={3,}|-{3,}|─{3,}|ㅡ{3,})\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
        static Regex selectSeparate3 = new Regex(@"^\s*─{3,}\s*$", RegexOptions.Compiled | RegexOptions.Multiline);

        private static async Task ReadNovel(string root, ViewItem item, string novelName)
        {
            if (File.Exists(item.file)) return;

            string html = "";

            for (int i = 0; i < 5; i++)
            {
                try
                {
                    html = await GetHtml(root + item.url);

                    if (html.Contains("캡챠를 통과해야 컨텐츠를 확인할 수 있습니다."))
                    {
                        if (!await AuthCapcha())
                        {
                            Console.WriteLine("캡챠 인증 후 엔터.");
                            Console.ReadLine();
                        }
                        i--;
                        RemoveCache(root + item.url);
                        continue;
                    }

                    if (html.Contains("\"novel_content\""))
                    {
                        break;
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }

                RemoveCache(root + item.url);
                await Task.Delay(1000);
                Log("wait for content");
            }

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            var novelContent = htmlDoc.DocumentNode.QuerySelector("article div#novel_content");

            var text = GetInnerTextWithNewLines(novelContent);
            text = selectSeparate1.Replace(text, "");
            text = text.Trim();

            var firstLineIndex = text.IndexOf('\n');
            if (firstLineIndex != -1)
            {
                var firstLine = text.Substring(0, firstLineIndex);
                if (firstLine.Last() == '\r') firstLine = firstLine.Substring(0, firstLine.Length - 1);

                if (firstLine.Contains(novelName))
                {
                    var match = Regex.Match(firstLine, @"(\d+)화");
                    if (match.Success)
                    {
                        text = $"제{match.Groups[1].Value}화.\r\n" + text.Substring(firstLineIndex + 1);
                    }
                    else
                    {
                        match = Regex.Match(firstLine, @"\d+");
                        if (match.Success)
                        {
                            text = $"제{match.Value}화.\r\n" + text.Substring(firstLineIndex + 1);
                        }
                    }
                }
            }

            text = NormalizeText(text);

            File.WriteAllText(item.file, text);
        }

        private static async Task<bool> AuthCapcha()
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    var driver = SeleniumHelper.driver;
                    if (driver == null) return false;

                    var imgElement = driver.FindElement(OpenQA.Selenium.By.CssSelector("#captcha > .captcha_img"));
                    if (imgElement == null) return false;

                    string script = @"
                    const img = arguments[0];
                    const canvas = document.createElement('canvas');
                    canvas.width = img.naturalWidth;
                    canvas.height = img.naturalHeight;
                    const ctx = canvas.getContext('2d');
                    ctx.drawImage(img, 0, 0);
                    return canvas.toDataURL('image/png');";

                    string dataUrl = (string)((IJavaScriptExecutor)driver).ExecuteScript(script, imgElement);

                    string? captchavalue = await AnalysisNumber.Analysis(dataUrl);
                    if (captchavalue == null) return false;

                    //"captcha_box"에 값 입력
                    var input = SeleniumHelper.driver!.FindElement(OpenQA.Selenium.By.CssSelector("#captcha_key"));
                    input.SendKeys(captchavalue);
                    Console.WriteLine($"Captcha '{captchavalue}'");

                    //form[name=fcaptcha] div.row button  클릭
                    var button = SeleniumHelper.driver!.FindElement(By.CssSelector("form[name=fcaptcha] div.row button"));
                    button.Click();

                    await Task.Delay(1000);

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
            return false;
        }

        static string GetInnerTextWithNewLines(HtmlNode node)
        {
            // 결과를 담을 변수
            var result = string.Empty;

            foreach (var child in node.ChildNodes)
            {
                if (child.Name == "br" || child.Name == "p" || child.Name == "div" || child.Name == "li")
                {
                    result += "\r\n\r\n";
                }

                // 텍스트 노드일 경우 텍스트를 추가
                if (child.NodeType == HtmlNodeType.Text)
                {
                    result += child.InnerText;
                }
                else
                {
                    // 자식 노드를 재귀적으로 처리
                    result += GetInnerTextWithNewLines(child);
                }
            }

            return HttpUtility.HtmlDecode(result.Trim());
        }

        private static CookieContainer? s_CookieContainer;
        private static HttpClientHandler? s_HttpClientHandler;
        private static HttpClient? s_HttpClient;
        private static DateTime? s_LastReadHttp;
        private static void RemoveCache(string url, string suffix = "")
        {
            var cacheFileName = "cache/cache" + Rename(url) + suffix;
            if (File.Exists(cacheFileName))
            {
                File.Delete(cacheFileName);
            }
        }
        private static async Task<string> GetHtml(string url, string suffix = "", bool useCache = true)
        {
            var cacheFileName = "cache/cache" + Rename(url) + suffix;
            try
            {
                bool exists = useCache && File.Exists(cacheFileName);
                if (exists)
                {
                    return await File.ReadAllTextAsync(cacheFileName);
                }
                else
                {
                    if (s_LastReadHttp != null)
                    {
                        while (true)
                        {
                            var elapsed = (DateTime.Now - s_LastReadHttp.Value).TotalMilliseconds;
                            var sleep = (httpDelay * 1000) - elapsed;
                            if (sleep > 100)
                            {
                                Console.Write($"\r남은 대기시간 : {sleep / 1000:f1}초    ");
                                await Task.Delay(100);
                            }
                            else if (sleep > 10)
                            {
                                Console.Write($"\r남은 대기시간 : {sleep / 1000:f1}초    ");
                                await Task.Delay((int)sleep);
                            }
                            else
                            {
                                Console.Write($"\r                            \r");
                                break;
                            }
                        }
                    }

                    s_LastReadHttp = DateTime.Now;

                    string html;
#if USE_SELENIUM
                    html = await SeleniumHelper.GetHtml(url);
#else
                InitHttpClient(url);
                html = await s_HttpClient!.GetStringAsync(url);
#endif

                    if (useCache)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(cacheFileName)!);
                        await File.WriteAllTextAsync(cacheFileName, html);
                    }

                    return html;
                }
            }
            catch
            {
                Console.WriteLine($"Error (url={url}, cache={cacheFileName})");
                throw;
            }
        }

        public static bool IsCandidate(string html)
        {
            return !(html.Contains("<title>잠시만 기다리십시오…</title>")
                    || html.Contains("사람인지 확인하는 중입니다. 이 작업은 몇 초 정도 소요될 수 있습니다.")
                    || html.Contains("<title>Just a moment...</title>"));
        }

        private static void InitHttpClient(string url)
        {
            if (s_HttpClient != null) return;

            s_CookieContainer = new CookieContainer();

            s_HttpClientHandler = new HttpClientHandler();
            s_HttpClientHandler.CookieContainer = s_CookieContainer;

            s_HttpClient = new HttpClient(s_HttpClientHandler);
            s_HttpClient.DefaultRequestHeaders.Pragma.Add(new System.Net.Http.Headers.NameValueHeaderValue("no-cache"));
            s_HttpClient.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue() { NoCache = true };
            s_HttpClient.DefaultRequestHeaders.AcceptLanguage.Clear();
            s_HttpClient.DefaultRequestHeaders.AcceptLanguage.Add(new("ko-KR"));
            s_HttpClient.DefaultRequestHeaders.AcceptLanguage.Add(new("ko", 0.9));
            s_HttpClient.DefaultRequestHeaders.AcceptLanguage.Add(new("en-US", 0.8));
            s_HttpClient.DefaultRequestHeaders.UserAgent.Clear();
            s_HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            s_HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AppleWebKit/537.36 (KHTML, like Gecko)");
            s_HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Chrome/128.0.0.0 Safari/537.36");

            string cookieString = File.ReadAllText(url);
            var cookies = cookieString.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Split('\t', StringSplitOptions.RemoveEmptyEntries));

            var uri = new Uri(url, UriKind.Absolute);
            uri = new Uri(uri.GetLeftPart(UriPartial.Authority), UriKind.Absolute);
            foreach (var cookie in cookies)
            {
                s_CookieContainer.Add(uri, new System.Net.Cookie(cookie[0], cookie[1]));
            }
        }

        private static string Rename(string url)
        {
            return Regex.Replace(url, @"[/\\:*?""<>|]", "_");
        }

        private static string NormalizeText(string text)
        {
            text = text.Replace("\r", "");
            text = Regex.Replace(text, @"\*{3,}", "***");
            text = text.Replace('"', '"');
            text = text.Replace('“', '"');
            text = text.Replace("…", "...");
            text = text.Replace("모든 노벨피 아 소설 획득 가능.", "...");
            text = Regex.Replace(text, "^.*https.*$", "");
            text = Regex.Replace(text, @"^[ -\)\+-/\[-`{-~:-@]+$", "", RegexOptions.Multiline);

            text = Regex.Replace(text, @"\n{3,}", "\n\n");
            return text;
        }

        private static string RenameArticle(string url)
        {
            return Regex.Replace(url, @"[\/:*?""<>|]", "_");
        }
    }
}
