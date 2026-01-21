using HtmlAgilityPack;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using ZubrSpbParserApp.Model;
using ZubrSpbParserApp.StringExtensions;

namespace ZubrSpbParserApp.BL
{
    public class ExportFormatter
    {
        private readonly string yandexDiskRoot;
        private readonly IReadOnlyList<Product> products;
        private readonly int startId;

        public ExportFormatter(IEnumerable<Product> products, int startId, string yandexDiskRoot)
        {
            this.products = products.ToList();
            this.startId = startId;
            this.yandexDiskRoot = yandexDiskRoot;

            FixEmptyProducts();

        }

        private void FixEmptyProducts()
        {
            if (products == null || products.Count == 0)
            {
                return;
            }

            var fixData = File.ReadAllLines("fix.txt")
                .Select(line => line.Split('\t'))
                .Select(parts => new ProductFixData
                {
                    Sku = parts[0],
                    Name = parts[1],
                    Manufacturer = parts[2]
                })
                .ToDictionary(i => i.Sku, i => i);

            foreach (var product in this.products)
            {
                if ((string.IsNullOrWhiteSpace(product.Name) || string.IsNullOrWhiteSpace(product.Manufacturer))
                    && fixData.TryGetValue(product.Sku, out var fix))
                {
                    product.Name = fix.Name;
                    product.Manufacturer = fix.Manufacturer;
                }
            }
        }

        public string GetGeneralExport()
        {
            var sb = new StringBuilder();

            int id = startId;
            foreach (var product in products)
            {
                string mainImage = GetMainImage(product);

                string caption = $"{product.Sku} {product.Manufacturer} {product.ProductType}";
                string keyword = Regex.Replace(Transliteration.Front(caption, true), "-{2,}", "-").Trim('-');
                string meta_title = $"{caption} купить в Санкт-Петербурге";
                string meta_desc = $"{meta_title} с доставкой по России";

                sb
                    .AppendTab(id.ToString())   //product_id
                    .AppendTab(product.Name)   //name(ru)
                    .AppendTab(string.Empty)   //categories
                    .AppendTab(product.Sku)   //sku
                    .AppendTab(string.Empty)   //upc
                    .AppendTab(string.Empty)   //ean
                    .AppendTab(string.Empty)   //jan
                    .AppendTab(string.Empty)   //isbn
                    .AppendTab(string.Empty)   //mpn
                    .AppendTab(string.Empty)   //location
                    .AppendTab("0")   //quantity
                    .AppendTab(product.Sku)   //model
                    .AppendTab(product.Manufacturer)   //manufacturer
                    .AppendTab(mainImage)   //image_name
                    .AppendTab("yes")   //shipping
                    .AppendTab("0")   //price
                    .AppendTab("0")   //points
                    .AppendTab(string.Empty)   //date_added
                    .AppendTab(string.Empty)   //date_modified
                    .AppendTab(string.Empty)   //date_available
                    .AppendTab("0")   //weight
                    .AppendTab("kg")   //weight_unit
                    .AppendTab("0")    //length
                    .AppendTab("0")    //width
                    .AppendTab("0")    //height
                    .AppendTab("mm")   //length_unit
                    .AppendTab("true")   //status
                    .AppendTab("0")   //tax_class_id
                    .AppendTab(keyword)   //seo_keyword
                    .AppendTab(string.Empty)   //description(ru)
                    .AppendTab("0")   //category_show(ru)
                    .AppendTab("0")   //main_product(ru)
                    .AppendTab(meta_title)   //meta_title(ru)
                    .AppendTab(meta_desc)   //meta_description(ru)
                    .AppendTab(string.Empty)   //meta_keywords(ru)
                    .AppendTab("10")   //stock_status_id
                    .AppendTab("0,1,2,3,4,5,6,7,8")   //store_ids
                    .AppendTab("0:,1:,2:,3:,4:,5:,6:,7:,8:")   //layout
                    .AppendTab(string.Empty)   //related_ids
                    .AppendTab(string.Empty)   //adjacent_ids
                    .AppendTab(string.Empty)   //tags(ru)
                    .AppendTab("1")   //sort_order
                    .AppendTab("true")   //subtract
                    .AppendLine("1"); //minimum

                id++;
            }

            var result = sb.ToString();
            return result;
        }

        public string GetAdditionalImagesExport()
        {
            var sb = new StringBuilder();

            int id = startId;
            foreach (var product in products)
            {
                int sort_order = 0;
                foreach (var image in product.Images.Skip(1))
                {
                    string imagePath = $"catalog/{product.ManufacturerFtpPath}/products/" + image.CreateMD5() + ".jpg";
                    sb.AppendLine($"{id}\t{imagePath}\t{sort_order++}");
                }

                id++;
            }

            var result = sb.ToString();
            return result;
        }

        public string GetDescriptionSql()
        {
            var sb = new StringBuilder();
            foreach (var product in products)
            {
                sb.Append($"UPDATE IGNORE oc_product_description ");
                sb.Append($"JOIN oc_product ON oc_product_description.product_id = oc_product.product_id ");
                sb.Append($"JOIN oc_manufacturer ON oc_product.manufacturer_id = oc_manufacturer.manufacturer_id ");
                sb.Append($"SET oc_product_description.description = '{HttpUtility.HtmlEncode(BuildDescriptionForProduct(product))}'");
                sb.AppendLine($"WHERE oc_manufacturer.name = '{product.Manufacturer}' AND (oc_product.model = '{product.Sku}' OR oc_product.sku = '{product.Sku}');");
            }

            return sb.ToString();
        }

        public string GetPdfSql()
        {
            var sb = new StringBuilder();

            sb.AppendLine("INSERT INTO oc_product_pdf (product_id, name, path) VALUES");

            int id = startId;
            foreach (var p in products)
            {
                foreach (var pdf in p.Instructions)
                {
                    string pdfMd5Name = pdf.Uri.CreateMD5() + ".pdf";
                    string path = $"{yandexDiskRoot}/{p.ManufacturerFtpPath}/{pdfMd5Name}";
                    sb.AppendLine($"({id}, '{pdf.Name}', '{path}'),");
                }
                id++;
            }

            var result = sb.ToString().Trim('\r', '\n', '\t', ' ', ',') + ";";

            return result;
        }

        private string GetMainImage(Product product)
        {
            string? firstImage = product.Images.FirstOrDefault();

            if (firstImage != null)
            {
                return $"catalog/{product.ManufacturerFtpPath}/products/" + firstImage.CreateMD5() + ".jpg";
            }
            else
            {
                return "catalog/placeholder.jpg";
            }
        }

        private string BuildDescriptionForProduct(Product product)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(product.DescriptionMarkup))
            {
                var desc = product.DescriptionMarkup;
                desc = Regex.Replace(desc, "<h4>(.*?)</h4>", "<p><strong>$1</strong></p>");
                desc = Regex.Replace(desc, " (style|class)=\"(.*?)\"", "");
                sb.AppendLine(desc);
            }
            if (product.Characteristics.Any())
            {
                sb.AppendLine("<h2>Технические характеристики</h2>");
                sb.AppendLine("<div class=\"table-responsive\">");
                sb.AppendLine("<table class=\"table\">");
                sb.AppendLine("<thead>");
                sb.AppendLine("<tr><th>Характеристика</th><th>Значение</th></tr>");
                sb.AppendLine("</thead>");
                sb.AppendLine("<tbody>");
                foreach (var characteristic in product.Characteristics)
                {
                    sb.AppendLine($"<tr><td>{characteristic.Name}</td><td>{characteristic.Value}</td></tr>");
                }
                sb.AppendLine("</tbody>");
                sb.AppendLine("</table>");
                sb.AppendLine("</div>");
            }

            string result = sb.ToString();

            var doc = new HtmlDocument();
            doc.LoadHtml(result);

            if (IsDocumentHasInvalidTags(doc))
            {
                throw new FormatException($"В описании товара '{product.Name ?? "<Без названия>"}' есть недопустимые теги: <a>, <img> или <iframe>");
            }

            return result;
        }

        private bool IsDocumentHasInvalidTags(HtmlDocument doc)
        {
            string[] invalidTags = new string[] { "img", "iframe", "a" };

            foreach (var tag in invalidTags)
            {
                if (doc.DocumentNode.SelectSingleNode($"//{tag}") != null)
                {
                    return true;
                }
            }

            return false;
        }

        internal string GetImagesSql()
        {
            var mainBuilder = new StringBuilder();
            var additionalBuilder = new StringBuilder("INSERT INTO oc_product_image (product_id, image, sort_order) VALUES\r\n");

            foreach (var product in products)
            {
                int sort_order = 0;

                foreach (var image in product.Images)
                {
                    string path = $"catalog/{product.ManufacturerFtpPath}/products/" + image.CreateMD5() + ".jpg";

                    if (sort_order == 0)
                    {
                        mainBuilder.Append($"UPDATE oc_product SET image = '{path}' ");
                        mainBuilder.AppendLine($"WHERE manufacturer_id = (SELECT manufacturer_id FROM oc_manufacturer WHERE name = '{product.Manufacturer}') AND (model = '{product.Sku}' OR sku = '{product.Sku}');");
                    }
                    else
                    {
                        string product_id = $"(SELECT product_id FROM oc_product WHERE manufacturer_id = (SELECT manufacturer_id FROM oc_manufacturer WHERE name = '{product.Manufacturer}') AND (model = '{product.Sku}' OR sku = '{product.Sku}') LIMIT 1)";
                        additionalBuilder.AppendLine($"({product_id}, '{path}', {sort_order}),");
                    }
                    sort_order++;
                }
            }

            var result = mainBuilder.ToString() + "\r\n" + additionalBuilder.ToString().Trim('\r', '\n', '\t', ',', ' ') + ";";
            return result;
        }

        internal string GetDimensionsSql()
        {
            var sb = new StringBuilder();
            foreach (var product in products)
            {
                string product_id = $"(SELECT product_id FROM oc_product WHERE manufacturer_id = (SELECT manufacturer_id FROM oc_manufacturer WHERE name = '{product.Manufacturer}') AND (model = '{product.Sku}' OR sku = '{product.Sku}') LIMIT 1)";

                var dimensionsChar = product.Characteristics.FirstOrDefault(c => c.Name == "Габариты (ДхШхВ)");
                
                if (dimensionsChar != null)
                {
                    dimensionsChar.Value = dimensionsChar.Value.Replace("&times;", "×");
                    Match dimensionsParts = Regex.Match(UnescapeString(dimensionsChar.Value), @"(?<length>\d+(\.\d+)?)×(?<width>\d+(\.\d+)?)×(?<height>\d+(\.\d+)?) (мм|см)");

                    if (dimensionsParts.Success)
                    {
                        decimal length = decimal.Parse(dimensionsParts.Groups["length"].Value.Replace(".", ",")) * 10;
                        decimal width = decimal.Parse(dimensionsParts.Groups["width"].Value.Replace(".", ",")) * 10;
                        decimal height = decimal.Parse(dimensionsParts.Groups["height"].Value.Replace(".", ",")) * 10;

                        sb.Append($"UPDATE IGNORE oc_product SET ")
                            .Append($"length = {length.ToString("F2").Replace(",", ".")},")
                            .Append($"width = {width.ToString("F2").Replace(",", ".")}, ")
                            .Append($"height = {height.ToString("F2").Replace(",", ".")} ");
                        sb.AppendLine($" WHERE product_id = {product_id};");
                    }
                    else
                    {
                        throw new FormatException(UnescapeString(dimensionsChar.Value));
                    }
                }

                var weightChar = product.Characteristics.FirstOrDefault(c => c.Name == "Вес");
                if (weightChar != null)
                {
                    Match weightPart = Regex.Match(UnescapeString(weightChar.Value), @"(?<weight>\d+(\.\d+)?)\s?(?<unit>кг)");
                    if (weightPart.Success)
                    {
                        decimal weight = decimal.Parse(weightPart.Groups["weight"].Value.Replace(".", ","));
                        if (weightPart.Groups["unit"].Value != "кг")
                        {
                            throw new FormatException("Неизвестная единицы веса");
                        }
                        sb.AppendLine($"UPDATE IGNORE oc_product SET weight = {weight.ToString("F4").Replace(",", ".")} WHERE product_id = {product_id};");
                    }
                    else
                    {
                        throw new FormatException(UnescapeString(weightChar.Value));
                    }
                }
            }
            string result = sb.ToString();
            return result;
        }
        private string UnescapeString(string input)
        {
            return input.Replace("&times;", "×");
        }
    }
}

public class ProductFixData
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
}
