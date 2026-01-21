using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ZubrSpbParserApp.Model
{
    public class Product
    {
        public string Uri { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string DescriptionMarkup { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string ProductType { get; set; } = string.Empty;

        public List<string> Images { get; set; } = new();
        public List<Pdf> Instructions { get; set; } = new();
        public List<Characteristic> Characteristics { get; set; } = new();

        [JsonIgnore]
        public string ManufacturerFtpPath
        {
            get
            {
                if (Regex.IsMatch(Manufacturer, @"[а-яА-Я]"))
                {
                    return Manufacturer switch
                    {
                        "ЗУБР" => "zubr",
                    };
                }
                else
                {
                    if (Manufacturer.Contains(" "))
                    {
                        throw new NotImplementedException();
                    }
                    return Manufacturer.ToLower();
                }
            }
        }
    }
}
