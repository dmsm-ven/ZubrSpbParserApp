using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ZubrSpbParserApp.Model;

namespace ZubrSpbParserApp.BL
{
    public class ParserManager : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly ProductParser parser;
        private readonly ResourceDownloader resourceDownloader;
        private readonly JsonSerializerOptions settings;
        private readonly string? yandexDiskRoot;
        private readonly string? resourcesRootFolder;
        private readonly string? storageFile;
        private List<Product>? products;

        private bool isParsingInProgress;
        public bool IsParsingInProgress
        {
            get => isParsingInProgress;
            private set
            {
                isParsingInProgress = value;
                OnPropertyChanged(nameof(IsParsingInProgress));
            }
        }

        public bool HasExportData
        {
            get => this.products != null && this.products.Any();
        }

        public ParserManager()
        {
            settings = new JsonSerializerOptions()
            {
                WriteIndented = true,
            };

            parser = new ProductParser();
            resourceDownloader = new ResourceDownloader();
        }

        public ParserManager(string storageFile, string yandexDiskRoot, string resourcesRootFolder) : this()
        {
            this.storageFile = storageFile;
            this.yandexDiskRoot = yandexDiskRoot;
            this.resourcesRootFolder = resourcesRootFolder;
        }

        public async Task ParseProducts(IProgress<double> progress)
        {
            IsParsingInProgress = true;
            try
            {
                if (!File.Exists("models_to_search.txt"))
                {
                    throw new Exception("File 'models_to_search.txt' not found. Please provide a file with models to search.");
                }
                string[] modelsToSearch = File.ReadAllLines("models_to_search.txt");

                if (modelsToSearch == null || modelsToSearch.Length == 0)
                {
                    throw new InvalidOperationException("No models to search provided.");
                }

                var list = await parser.ParseProducts(modelsToSearch, progress);
                await Save(list);
                products = list;

            }
            catch
            {
                throw;
            }
            finally
            {
                IsParsingInProgress = false;
            }
        }

        public string GetGeneralExport(int pid)
        {
            return GetFormatter(pid).GetGeneralExport();
        }

        public string GetDescriptionSql(int pid)
        {
            return GetFormatter(pid).GetDescriptionSql();
        }

        public string GetAdditionalImagesExport(int pid)
        {
            return GetFormatter(pid).GetAdditionalImagesExport();
        }

        public string GetPdfSql(int pid)
        {
            return GetFormatter(pid).GetPdfSql();
        }

        private async Task Save(List<Product> productsToSave)
        {
            if (productsToSave == null)
            {
                throw new ArgumentNullException(nameof(productsToSave));
            }

            try
            {
                using (var fs = File.Create(this.storageFile))
                {
                    await JsonSerializer.SerializeAsync<List<Product>>(fs, productsToSave, settings);
                }
            }
            catch
            {
                throw;
            }
        }

        public async Task Load()
        {
            if (!File.Exists(storageFile))
            {
                return;
            }
            try
            {
                using (var fs = File.OpenRead(storageFile))
                {
                    products = await JsonSerializer.DeserializeAsync<List<Product>>(fs, settings);
                    OnPropertyChanged(nameof(HasExportData));
                }
            }
            catch
            {
                throw;
            }
        }

        public async Task DownloadResources(IProgress<double> progress)
        {
            await resourceDownloader.DownloadResource(products, resourcesRootFolder, progress);
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private ExportFormatter GetFormatter(int startId)
        {
            if (startId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startId));
            }

            return new ExportFormatter(products, startId, yandexDiskRoot);
        }

        internal string GetImagesSql(int pid)
        {
            return GetFormatter(pid).GetImagesSql();
        }

        internal string GetDimensionsSql(int pid)
        {
            return GetFormatter(pid).GetDimensionsSql();
        }
    }
}
