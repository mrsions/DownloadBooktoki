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
    public class Program
    {
        static async Task Main(string[] args)
        {
            //await AnalysisNumber.Analysis("https://booktoki468.com/plugin/kcaptcha/kcaptcha_image.php?t=1743070238722")
            await App.Start(args);
        }
    }
}
