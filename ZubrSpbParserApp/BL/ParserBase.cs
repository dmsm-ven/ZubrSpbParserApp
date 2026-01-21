using System.Net;
using System.Net.Http;
using HtmlAgilityPack;

namespace ZubrSpbParserApp.BL
{
    public abstract class ParserBase
    {
        private readonly HttpClient client;

        public ParserBase()
        {
            var handler = new HttpClientHandler()
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseCookies = true,
                CookieContainer = new CookieContainer()
            };

            client = new HttpClient(handler);
        }

        protected void AddHeaders(IEnumerable<KeyValuePair<string, string>> headers)
        {
            foreach (var kvp in headers)
            {
                client.DefaultRequestHeaders.Add(kvp.Key, kvp.Value);
            }
        }

        protected virtual async Task<HtmlDocument> GetDocument(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri) || !uri.StartsWith("http"))
            {
                return null;
            }

            try
            {
                var doc = new HtmlDocument();

                var str = await client.GetStringAsync(uri);

                doc.LoadHtml(str);
                return doc;
            }
            catch
            {
                return null;
            }
        }
    }
}
