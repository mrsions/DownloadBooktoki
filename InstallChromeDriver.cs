using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DownloadBooktoki
{
    public class InstallChromeDriver
    {
        public static async Task InstallAsync()
        {
            string chromeVersion = GetInstalledChromeVersion();
            if (!string.IsNullOrEmpty(chromeVersion))
            {
                string majorVersion = chromeVersion.Split('.')[0];  // 주 버전 추출
                Console.WriteLine($"Installed Chrome version: {chromeVersion} (Major: {majorVersion})");

                // CfT JSON 엔드포인트
                string jsonUrl = "https://googlechromelabs.github.io/chrome-for-testing/latest-versions-per-milestone-with-downloads.json";

                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(jsonUrl);
                    response.EnsureSuccessStatusCode();
                    string jsonString = await response.Content.ReadAsStringAsync();

                    // JSON 파싱
                    var jsonData = JsonDocument.Parse(jsonString);

                    // 주어진 majorVersion에 맞는 다운로드 URL 가져오기
                    if (jsonData.RootElement.TryGetProperty("milestones", out var milestonesElement) &&
                        milestonesElement.TryGetProperty(majorVersion, out var versionElement))
                    {
                        var chromedriverElement = versionElement.GetProperty("downloads").GetProperty("chromedriver");

                        // OS에 맞는 ChromeDriver URL 추출
                        string platform = GetPlatform();
                        foreach (var driver in chromedriverElement.EnumerateArray())
                        {
                            if (driver.GetProperty("platform").GetString() == platform)
                            {
                                string downloadUrl = driver.GetProperty("url").GetString();
                                Console.WriteLine($"ChromeDriver {majorVersion} 다운로드 중... URL: {downloadUrl}");

                                // ChromeDriver 다운로드 및 저장
                                string driverPath = "chromedriver.zip";
                                var driverData = await client.GetByteArrayAsync(downloadUrl);
                                await File.WriteAllBytesAsync(driverPath, driverData);

                                // 압축 해제
                                Console.WriteLine("압축 해제 중...");
                                System.IO.Compression.ZipFile.ExtractToDirectory(driverPath, Directory.GetCurrentDirectory(), true);
                                Console.WriteLine("ChromeDriver 다운로드 및 설치 완료.");
                                return;
                            }
                        }
                    }
                }
            }

            Console.WriteLine("Chrome이 설치되어 있지 않거나 버전을 가져올 수 없습니다.");
            Console.ReadLine();
            Environment.Exit(1);
        }

        static string GetPlatform()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Environment.Is64BitOperatingSystem ? "win64" : "win32";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "mac-arm64" : "mac-x64";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "linux64";
            }
            throw new PlatformNotSupportedException("지원되지 않는 운영체제입니다.");
        }


        // 크로스 플랫폼에서 Chrome 버전 가져오기
        static string GetInstalledChromeVersion()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetChromeVersionWindows();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetChromeVersionMac();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetChromeVersionLinux();
            }
            else
            {
                throw new PlatformNotSupportedException("이 운영체제는 지원되지 않습니다.");
            }
        }

        static string GetChromeVersionWindows()
        {
            string chromePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
            if (File.Exists(chromePath))
            {
                return FileVersionInfo.GetVersionInfo(chromePath)?.FileVersion ?? "";
            }
            return "";
        }

        static string GetChromeVersionMac()
        {
            string chromePath = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
            return GetChromeVersionFromCommand(chromePath);
        }

        static string GetChromeVersionLinux()
        {
            string chromePath = "/usr/bin/google-chrome";
            return GetChromeVersionFromCommand(chromePath);
        }

        static string GetChromeVersionFromCommand(string chromePath)
        {
            try
            {
                Process process = new Process();
                process.StartInfo.FileName = chromePath;
                process.StartInfo.Arguments = "--version";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output.Trim().Replace("Google Chrome", "").Trim();
            }
            catch
            {
                return "";
            }
        }
    }
}
