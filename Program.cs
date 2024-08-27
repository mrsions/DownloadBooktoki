using System;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Web;

using Fizzler.Systems.HtmlAgilityPack;

using HtmlAgilityPack;

namespace DownloadBooktoki
{
    public record class ViewItem(string url, string name, string file)
    {
    }

    public class Program
    {
        private const string MERGE_PATH = "Novels/Merges";
        public static string? CookieStr;
        public static int httpDelay = 1;

        static async Task Main(string[] args)
        {
            while (true)
            {
                Console.WriteLine("----------------------------");
                Console.WriteLine("1. 북토끼 링크 열기");
                Console.WriteLine("2. 시작");
                Console.WriteLine("3. 합치기");

                var selectText = Console.ReadLine();
                Console.WriteLine();
                if (selectText != null && int.TryParse(selectText.Trim(), out int select))
                {
                    switch (select)
                    {
                        case 1:
                            try
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = "https://t.me/s/newtoki5?before=90",
                                    UseShellExecute = true
                                });
                            }
                            catch
                            {
                                Console.WriteLine($"실행 실패. 'https://t.me/s/newtoki5?before=90'로 직접들어가시거나 북토끼 사이트를 찾아주세요.");
                            }
                            break;

                        case 2:
                            await Download();
                            break;

                        case 3:
                            await MergeAll();
                            break;
                    }
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

                        writer.WriteLine(File.ReadAllText(files[i]));
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

        private static async Task Download()
        {
            Console.Clear();

            CookieStr = File.ReadAllText("config/cookie.txt");
            httpDelay = int.Parse(File.ReadAllText("config/delay.txt"));

            string[] hrefs = File.ReadAllLines("config/href.txt").Where(v=>v.StartsWith("http")).Distinct().ToArray();
            for (int i = 0; i < hrefs.Length; i++)
            {
                string? href = hrefs[i];
                Uri? uri = new Uri(href);
                InitHttpClient(uri);

                var read = await ReadList(uri.PathAndQuery);
                var novelName = read.Item1;
                var items = read.Item2;

                Log($"[{i}/{hrefs.Length}] '{novelName}' (총 {items.Count}개)");

                items.Reverse();
                for (int j = 0; j < items.Count; j++)
                {
                    ViewItem? item = items[j];

                    long remainTime = (items.Count - j) * (httpDelay + 1) * TimeSpan.TicksPerSecond;

                    Log($"[{i}/{hrefs.Length}] '{novelName}' {item.file} ({j}/{items.Count}, 남은시간: {new TimeSpan(remainTime)})");
                    await ReadNovel(item, novelName);
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

        private static string ReadLine(string beforePath)
        {
            string defaultText = ""; // 미리 입력해둘 텍스트
            if (File.Exists(beforePath))
            {
                defaultText = File.ReadAllText(beforePath);
            }
            StringBuilder input = new StringBuilder(defaultText);

            Console.Write("\r" + defaultText); // 텍스트를 미리 콘솔에 출력

            while (true)
            {
                var key = Console.ReadKey(true); // 키를 숨기고 입력 받기

                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine(); // Enter 키가 눌리면 입력 종료
                    break;
                }
                else if (key.Key == ConsoleKey.Escape)
                {
                    // Esc 키가 눌리면 입력 초기화
                    Console.Write("\r"); // 안내문 출력
                    for (int i = 0; i < input.Length; i++)
                    {
                        Console.Write(" ");
                    }
                    Console.Write("\r"); // 안내문 출력
                    input.Clear();
                }
                else if (key.Key == ConsoleKey.Backspace && input.Length > 0)
                {
                    // 백스페이스 처리
                    input.Remove(input.Length - 1, 1);
                    Console.Write("\b \b"); // 콘솔에서 문자 제거
                }
                else if (key.Key != ConsoleKey.Backspace)
                {
                    // 일반 문자 입력 처리
                    input.Append(key.KeyChar);
                    Console.Write(key.KeyChar);
                }
            }

            var rst = input.ToString().Trim();
            File.WriteAllText(beforePath, rst);
            return rst;
        }

        private static async Task<(string, List<ViewItem>)> ReadList(string listUrl)
        {
            List<ViewItem> items = new();

            var html = await GetHtml(listUrl, $"{DateTime.Now:yyyyMMddHH}.list");

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
                        var file = $"{dir}/{RenameArticle(name)}.txt";

                        items.Add(new(href, name, file));
                    }
                }
            }

            return (novelName, items);
        }

        static Regex selectSeparate1 = new Regex(@"^\s*(={3,}|-{3,}|─{3,}|ㅡ{3,})\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
        static Regex selectSeparate3 = new Regex(@"^\s*─{3,}\s*$", RegexOptions.Compiled | RegexOptions.Multiline);

        private static async Task ReadNovel(ViewItem item, string novelName)
        {
            if (File.Exists(item.file)) return;

            var html = await GetHtml(item.url);

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


            File.WriteAllText(item.file, text);
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
        private static async Task<string> GetHtml(string url, string suffix = "")
        {
            var cacheFileName = "cache" + Rename(url) + suffix;
            bool exists = File.Exists(cacheFileName);
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

                var html = await s_HttpClient!.GetStringAsync(url);
                Directory.CreateDirectory(Path.GetDirectoryName(cacheFileName)!);
                await File.WriteAllTextAsync(cacheFileName, html);
                s_LastReadHttp = DateTime.Now;

                return html;
            }
        }

        private static void InitHttpClient(Uri uri)
        {
            if (s_HttpClient != null) return;

            s_CookieContainer = new CookieContainer();

            s_HttpClientHandler = new HttpClientHandler();
            s_HttpClientHandler.CookieContainer = s_CookieContainer;

            s_HttpClient = new HttpClient(s_HttpClientHandler);
            s_HttpClient.BaseAddress = new Uri(uri.GetLeftPart(UriPartial.Authority));
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

            var cookies = CookieStr.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Split('\t', StringSplitOptions.RemoveEmptyEntries));

            foreach (var cookie in cookies)
            {
                s_CookieContainer.Add(s_HttpClient.BaseAddress, new Cookie(cookie[0], cookie[1]));
            }
        }

        private static string Rename(string url)
        {
            return Regex.Replace(url, @"[:*?""<>|]", "_");
        }

        private static string RenameArticle(string url)
        {
            return Regex.Replace(url, @"[\/:*?""<>|]", "_");
        }
    }
}
