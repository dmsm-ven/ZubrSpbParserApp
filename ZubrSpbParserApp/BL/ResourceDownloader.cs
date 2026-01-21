using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using ZubrSpbParserApp.Model;
using ZubrSpbParserApp.StringExtensions;

namespace ZubrSpbParserApp.BL;

public class ResourceDownloader
{
    private readonly HttpClient httpClient = new HttpClient(new HttpClientHandler()
    {
        AllowAutoRedirect = true,
        CookieContainer = new CookieContainer()
    });
    public async Task DownloadResource(IEnumerable<Product> products, string folder, IProgress<double> indicator)
    {
        int total = products.Count();
        int current = 0;

        foreach (var product in products)
        {

            try
            {
                await DownloadProductResources(folder, product);
            }
            catch
            {

            }

            indicator?.Report((double)++current / total);
        }
    }

    private async Task DownloadProductResources(string folder, Product product)
    {
        var images = product.Images.Distinct().ToArray();

        foreach (var image in images)
        {
            string localPath = Path.Combine(folder, product.ManufacturerFtpPath, "products", image.CreateMD5() + Path.GetExtension(image));

            if (File.Exists(localPath))
            {
                continue;
            }

            var dir = Path.GetDirectoryName(localPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            try
            {
                var bytes = await httpClient.GetByteArrayAsync(image);
                await File.WriteAllBytesAsync(localPath, bytes);
            }
            catch
            {
                Debug.WriteLine($"Error downloading image: {image}");
            }
        }

        var instructions = product.Instructions;

        foreach (var pdf in instructions)
        {
            string localPath = Path.Combine(folder, "pdf", pdf.Uri.CreateMD5() + Path.GetExtension(pdf.Uri));

            if (File.Exists(localPath))
            {
                continue;
            }

            var dir = Path.GetDirectoryName(localPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            try
            {
                var bytes = await httpClient.GetByteArrayAsync(pdf.Uri);
                await File.WriteAllBytesAsync(localPath, bytes);
            }
            catch
            {
                Debug.WriteLine("Error downloading pdf: " + pdf.Uri);
            }
        }

    }
}