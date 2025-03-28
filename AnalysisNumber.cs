using System.Net;
using System.Text.RegularExpressions;
using GenerativeAI;
using GenerativeAI.Types;

namespace DownloadBooktoki
{
    public class AnalysisNumber
    {
        static string instruction = @"
너는 이미지에서 숫자를 추출하는 전문가야. 
입력된 이미지에서 숫자만 대답해줘.

# 대답예시
1234
4567
8762";

        public static async Task<string?> Analysis(string url)
        {
            if (url == null) return null;

            string imgpath;
            if (url.StartsWith("data:image"))
            {
                string base64Data = url.Substring(url.IndexOf(",") + 1);
                byte[] imageBytes = Convert.FromBase64String(base64Data);

                if (imageBytes.Take(4).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }))
                { 
                    imgpath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".png");
                }
                else
                {
                    imgpath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".jpg");
                }

                await File.WriteAllBytesAsync(imgpath, imageBytes);
            }
            else
            {
                imgpath = await DownloadFile(url);
            }

            var gemini = new GoogleAi("AIzaSyAoW8PP_ReWL_wVLpY3T-CqZM86g9ZHALk");
            var model = gemini.CreateGenerativeModel("models/gemini-1.5-flash", systemInstruction: instruction);

            var request = new GenerateContentRequest();

            request.AddInlineFile(imgpath);

            var response = await model.GenerateContentAsync(request);
            string? result = Regex.Replace(response.Text(), @"[\r\n ]+", "");

            return result;
        }

        public static async Task<string> DownloadFile(string url)
        {
            // TLS 버전 설정
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            using (HttpClient client = new HttpClient(handler))
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                try
                {
                    var response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var data = await response.Content.ReadAsByteArrayAsync();

                    var extension = Path.GetExtension(new Uri(url).LocalPath);
                    if (string.IsNullOrWhiteSpace(extension))
                        extension = ".img";

                    string tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + extension);
                    await File.WriteAllBytesAsync(tempFile, data);

                    return tempFile;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"다운로드 중 오류 발생: {ex}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"내부 예외: {ex.InnerException.Message}");
                    }
                    return null;
                }
            }
        }


    }
}
