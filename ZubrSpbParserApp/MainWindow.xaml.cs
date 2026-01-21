using System.IO;
using System.Windows;
using ZubrSpbParserApp.BL;

namespace ZubrSpbParserApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ParserManager manager;

        public int PID
        {
            get
            {
                if (int.TryParse(txtStartPID.Text, out var pid))
                {
                    return pid;
                }

                return -1;
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            manager = new ParserManager(
                storageFile: "products.json",
                yandexDiskRoot: "https://disk.yandex.ru/d/V1KNVJO3SY3ROw",
                resourcesRootFolder: Path.Combine(Directory.GetCurrentDirectory(), "resources"));
            DataContext = manager;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await manager.Load();
            MessageBox.Show("Сделать фикс названий брендов (неправильный регистр, например KRAFTOOL вместо Kraftool) при экспорте");
        }

        private async void btnStartParse_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await manager.ParseProducts(new Progress<double>(v => pbIndicator.Value = v));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n\r\n" + ex.StackTrace, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnDownloadResources_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await manager.DownloadResources(new Progress<double>(v => pbIndicator.Value = v));
            }
            catch (Exception ex)
            {
                ShowErrorMessage(ex);
            }
        }

        private void btnGeneralExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string text = manager.GetGeneralExport(PID);
                Clipboard.SetText(text);
                ShowInformationMessage(text.Length);
            }
            catch (Exception ex)
            {
                ShowErrorMessage(ex);
            }
        }

        private void btnExportAdditionalImages_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string text = manager.GetAdditionalImagesExport(PID);
                Clipboard.SetText(text);
                ShowInformationMessage(text.Length);
            }
            catch (Exception ex)
            {
                ShowErrorMessage(ex);
            }
        }

        private void btnExportDescription_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string text = manager.GetDescriptionSql(PID);
                Clipboard.SetText(text);
                ShowInformationMessage(text.Length);
            }
            catch (Exception ex)
            {
                ShowErrorMessage(ex);
            }
        }

        private void btnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string text = manager.GetPdfSql(PID);
                Clipboard.SetText(text);
                ShowInformationMessage(text.Length);
            }
            catch (Exception ex)
            {
                ShowErrorMessage(ex);
            }
        }

        private void ShowErrorMessage(Exception ex)
        {
            MessageBox.Show(ex.Message + "\r\n\r\n" + ex.StackTrace, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ShowInformationMessage(int messageLength)
        {

            MessageBox.Show($"Информация вставлена в буфер обмена (длина строки {messageLength} символов)", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnExportImages_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string text = manager.GetImagesSql(PID);
                Clipboard.SetText(text);
                ShowInformationMessage(text.Length);
            }
            catch (Exception ex)
            {
                ShowErrorMessage(ex);
            }
        }

        private void btnExportDimensions_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string text = manager.GetDimensionsSql(PID);
                Clipboard.SetText(text);
                ShowInformationMessage(text.Length);
            }
            catch (Exception ex)
            {
                ShowErrorMessage(ex);
            }
        }
    }
}