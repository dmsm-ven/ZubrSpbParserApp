using System.Text.RegularExpressions;
using System.Web;
using ZubrSpbParserApp.Model;
using ZubrSpbParserApp.StringExtensions;

namespace ZubrSpbParserApp.BL
{
    public class ProductParser : ParserBase
    {
        public static readonly string HOST = "https://zubrspb.ru";

        public async Task<List<Product>> ParseProducts(IEnumerable<string> skus, IProgress<double> progress = null)
        {
            var parsedProducts = new List<Product>();

            int total = skus.Count();
            int current = 0;

            foreach (var sku in skus)
            {
                var product = await SearchProduct(sku);
                if (!string.IsNullOrWhiteSpace(product.Uri))
                {
                    await ParseProductDetails(product);
                }
                await Task.Delay(TimeSpan.FromSeconds(1));

                parsedProducts.Add(product);

                progress?.Report((double)++current / total);
            }

            return parsedProducts;
        }

        private async Task<Product> SearchProduct(string sku)
        {
            var url = $"{HOST}/search/?search={HttpUtility.UrlEncode(sku)}";

            var doc = await GetDocument(url);

            var searchResultUri = doc.DocumentNode.SelectSingleNode($"//div[@class='product-thumb__model' and text()='{sku}']/../a");
            string href = searchResultUri?.GetAttributeValue("href", null) ?? string.Empty;
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

            product.Name = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText.Trim() ?? string.Empty;
            product.Manufacturer = doc.DocumentNode
                .SelectSingleNode("//li[@class='product-data__item manufacturer']//img")
                ?.GetAttributeValue("title", null) ?? throw new Exception("cannot read brand info");

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

            var characteristicsXPatch = "//div[@id='tab-specification']//li[contains(@class, 'product-data__item')]";
            if (doc.DocumentNode.SelectSingleNode(characteristicsXPatch) != null)
            {
                var charLiItems = doc.DocumentNode.SelectNodes(characteristicsXPatch)
                    .Select(li => new Characteristic()
                    {
                        Name = li.SelectSingleNode("./div[1]")?.InnerText.TrimHtml() ?? string.Empty,
                        Value = li.SelectSingleNode("./text()")?.InnerText.TrimHtml() ?? string.Empty
                    })
                    .Where(i => !string.IsNullOrWhiteSpace(i.Name) && !string.IsNullOrWhiteSpace(i.Value))
                    .ToArray();

                product.Characteristics.AddRange(charLiItems);
            }
        }
    }
}
