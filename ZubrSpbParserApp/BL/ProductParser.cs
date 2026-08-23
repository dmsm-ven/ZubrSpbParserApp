using HtmlAgilityPack;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Web;
using ZubrSpbParserApp.Model;
using ZubrSpbParserApp.StringExtensions;

namespace ZubrSpbParserApp.BL
{
    public class ProductParser : ParserBase
    {
        private readonly HttpClient searchClient;
        public ProductParser()
        {
            HttpClientHandler searchHttpClientHandler = new HttpClientHandler
            {
                UseCookies = true, // соответствует "credentials": "include"
                CookieContainer = new System.Net.CookieContainer()
            };
            searchClient = new HttpClient(searchHttpClientHandler);
        }
        public static readonly string HOST = "https://zubr-instrument-tech.ru";

        public async Task<List<Product>> ParseProducts(IEnumerable<string> skus, IProgress<(double, string)> progress)
        {
            var parsedProducts = new List<Product>();

            int total = skus.Count();
            int current = 0;

            foreach (var sku in skus)
            {
                string statusString = $"Поиск товара с SKU {sku}";
                var product = await SearchProduct(sku);
                if (!string.IsNullOrWhiteSpace(product?.Uri))
                {
                    await ParseProductDetails(product);
                }
                statusString += " | " + ((string.IsNullOrWhiteSpace(product.Uri) ? "товар не найден" : "OK"));
                progress?.Report(new((double)++current / total, statusString));

                await Task.Delay(TimeSpan.FromSeconds(0.33));

                parsedProducts.Add(product);
            }

            return parsedProducts;
        }

        private async Task<Product> SearchProduct(string sku)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{HOST}/index.php?route=extension/module/uni_live_search");

            // Заголовки запроса
            request.Headers.Accept.ParseAdd("text/html, */*; q=0.01");
            request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9,ru;q=0.8");
            request.Headers.CacheControl = CacheControlHeaderValue.Parse("no-cache");
            request.Headers.Pragma.ParseAdd("no-cache");
            request.Headers.Add("sec-ch-ua", "\"Not=A?Brand\";v=\"99\", \"Google Chrome\";v=\"151\", \"Chromium\";v=\"151\"");
            request.Headers.Add("sec-ch-ua-mobile", "?0");
            request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.Add("sec-fetch-dest", "empty");
            request.Headers.Add("sec-fetch-mode", "cors");
            request.Headers.Add("sec-fetch-site", "same-origin");
            request.Headers.Add("x-requested-with", "XMLHttpRequest");
            request.Headers.Referrer = new Uri(HOST);

            // Тело запроса + content-type
            var content = new StringContent($"filter_name={sku}&category_id=");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded")
            {
                CharSet = "UTF-8"
            };
            request.Content = content;

            var response = await searchClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(responseBody);

            string safeCode = sku.Replace("'", "\\'");

            string xpath = $@"
        //li[contains(concat(' ', normalize-space(@class), ' '), ' live-search__item ')]
        [.//div[contains(concat(' ', normalize-space(@class), ' '), ' live-search__model ')]
            [normalize-space(substring-after(text(), 'Код товара:')) = '{safeCode}']
        ]";

            var href = doc.DocumentNode.SelectSingleNode(xpath)?.GetAttributeValue("data-href", string.Empty) ?? string.Empty;
            return new Product()
            {
                Uri = href,
                Sku = sku
            };
        }

        private async Task ParseProductDetails(Product product)
        {
            var doc = await GetDocument(product.Uri);
            if (doc == null)
            {
                return;
            }

            var productCode = doc.DocumentNode.SelectSingleNode("//div[@class='rating-model__model']")?.InnerText.TrimHtml().Replace("Код товара: ", string.Empty);
            if (productCode != product.Sku)
            {
                throw new Exception("Ошибка несовпадения SKU при поиске и внутри карточки товара");
            }

            product.Name = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText.Trim() ?? string.Empty;
            product.Manufacturer = doc.DocumentNode
                .SelectSingleNode("//div[@class='product-data__item manufacturer']/a")
                ?.InnerText.TrimHtml();

            product.DescriptionMarkup = doc.DocumentNode
                .SelectSingleNode("//div[@id='tab-description']")?.InnerHtml.Trim() ?? string.Empty;

            string imgXPatch = "//div[@class='product-page__image-main']//img[@data-thumb]";
            if (doc.DocumentNode.SelectSingleNode(imgXPatch) != null)
            {
                var productImages = doc.DocumentNode.SelectNodes(imgXPatch)
                    .Select(img => img.GetAttributeValue("src", null))
                    .Where(src => !string.IsNullOrWhiteSpace(src))
                    .Select(src => Regex.Replace(src, @"^(.*?)/cache/(.*?)-\d+x\d+(.*)$", "$1/$2$3"))
                    .ToArray();
                product.Images.AddRange(productImages);
            }
            else
            {

            }

            var characteristicsXPatch = "//div[@id='tab-specification']/div[@class='product-data']";
            if (doc.DocumentNode.SelectSingleNode(characteristicsXPatch) != null)
            {
                var groups = doc.DocumentNode.SelectNodes(characteristicsXPatch).ToArray();

                foreach (var groupDiv in groups)
                {
                    var groupName = groupDiv.SelectSingleNode("preceding-sibling::h4[1]")?.InnerText.TrimHtml();
                    var groupItems = groupDiv.SelectNodes("./div[contains(@class, 'product-data__item')]")
                        .Select(row => new Characteristic()
                        {
                            Group = groupName,
                            Name = row.SelectSingleNode("./div")?.InnerText.TrimHtml(),
                            Value = HttpUtility.HtmlDecode(row.SelectSingleNode("./text()")?.InnerText.TrimHtml())
                        })
                        .ToArray();

                    product.Characteristics.AddRange(groupItems);
                }

                var dimensionsCharNode = product.Characteristics.FirstOrDefault(c => c.Name == "Габариты (ДхШхВ)");
                if (dimensionsCharNode != null)
                {
                    string pattern = @"(?<length>\d+(?:[.,]\d+)?)\s*[×xXхХ]\s*(?<width>\d+(?:[.,]\d+)?)\s*[×xXхХ]\s*(?<height>\d+(?:[.,]\d+)?)\s*(?<unit>[a-zA-Zа-яА-Я]+)?";

                    Match match = Regex.Match(dimensionsCharNode.Value, pattern);

                    string unit = match.Groups["unit"].Success ? match.Groups["unit"].Value : "";
                    if (unit != "см")
                    {
                        throw new FormatException($"Неизвестная мера длины: {unit}");
                    }

                    product.Length = decimal.Parse(match.Groups["length"].Value.Replace(".", ","));
                    product.Width = decimal.Parse(match.Groups["width"].Value.Replace(".", ","));
                    product.Height = decimal.Parse(match.Groups["height"].Value.Replace(".", ","));
                }

                var weightCharNode = product.Characteristics.FirstOrDefault(c => c.Name == "Вес");
                if (weightCharNode != null)
                {
                    string pattern = @"(?<value>\d+(?:[.,]\d+)?)\s*(?<unit>[a-zA-Zа-яА-Я]+)?";
                    Match match = Regex.Match(weightCharNode.Value, pattern);

                    string unit = match.Groups["unit"].Success ? match.Groups["unit"].Value : "";
                    if (unit != "кг")
                    {
                        throw new FormatException($"Неизвестная мера веса: {unit}");
                    }

                    product.Weight = decimal.Parse(match.Groups["value"].Value.Replace(".", ","));

                }
            }
        }
    }
}
