using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace RGHAudioConverter
{
    public partial class MainWindow : Window
    {
        private readonly Random _random = new Random();
        private readonly string[] _failedImages = 
        {
            "Contents/Gui/Icons/Rabbid_Failed.png",
            "Contents/Gui/Icons/Rabbid_Failed_2.png",
            "Contents/Gui/Icons/Rabbid_Failed_3.png"
        };
        private const string SuccessImage = "Contents/Gui/Icons/Rabbid_Success.png";
        private readonly string _baseDir;
        private FontFamily? _rabbidsFont;

        public MainWindow()
        {
            InitializeComponent();
            
            _baseDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // Load custom font
            LoadCustomFont();
            
            // Load images at startup
            LoadImages();
        }

        private void LoadCustomFont()
        {
            try
            {
                string fontDir = Path.Combine(_baseDir, "Contents", "Font");
                if (Directory.Exists(fontDir))
                {
                    _rabbidsFont = new FontFamily(new Uri(fontDir + "/"), "./#Rabbids_go_home");
                    
                    this.FontFamily = _rabbidsFont;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Font loading failed: {ex.Message}");
            }
        }

        private void LoadImages()
        {
            try
            {
                // Load logo
                string logoPath = Path.Combine(_baseDir, "Contents", "Gui", "RGH logo", "RGHAC.png");
                if (File.Exists(logoPath))
                    LogoImage.Source = LoadBitmapImage(logoPath);

                // Load NGC button image
                string ngcPath = Path.Combine(_baseDir, "Contents", "Gui", "NGC", "NGC.png");
                if (File.Exists(ngcPath))
                    NgcImage.Source = LoadBitmapImage(ngcPath);

                // Load OGG button image
                string oggPath = Path.Combine(_baseDir, "Contents", "Gui", "OGG", "OGGFish.png");
                if (File.Exists(oggPath))
                    OggImage.Source = LoadBitmapImage(oggPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading images: {ex.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private BitmapImage? LoadBitmapImage(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(path);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    return bitmap;
                }
            }
            catch { }
            return null;
        }

        private BitmapImage? LoadImage(string relativePath)
        {
            string fullPath = Path.Combine(_baseDir, relativePath);
            return LoadBitmapImage(fullPath);
        }

        private async void NgcButton_Click(object sender, RoutedEventArgs e)
        {
            await ConvertAudio(CodecType.DSP);
        }

        private async void OggButton_Click(object sender, RoutedEventArgs e)
        {
            await ConvertAudio(CodecType.OGG);
        }

        private void UpdateProgress(int percent)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"Encoding... {percent}%";
            });
        }

        private bool IsDebugMode => DebugCheckbox.IsChecked == true;

        private async Task ConvertAudio(CodecType codec)
        {
            // Open file dialog for WAV
            var openDialog = new OpenFileDialog
            {
                Title = "Select WAV File",
                Filter = "WAV files (*.wav)|*.wav|All files (*.*)|*.*"
            };

            if (openDialog.ShowDialog() != true)
                return;

            string inputPath = openDialog.FileName;

            // Validate WAV file
            if (!LynEncoder.ValidateWavFile(inputPath, out string validationError))
            {
                ShowResult(false, validationError);
                return;
            }

            // Save file dialog for SNS
            var saveDialog = new SaveFileDialog
            {
                Title = "Save SNS File",
                Filter = "SNS files (*.sns)|*.sns|All files (*.*)|*.*",
                FileName = Path.GetFileNameWithoutExtension(inputPath) + ".sns"
            };

            if (saveDialog.ShowDialog() != true)
                return;

            string outputPath = saveDialog.FileName;

            // Disable buttons and show status
            SetButtonsEnabled(false);
            StatusText.Text = "Encoding... 0%";

            bool debugMode = IsDebugMode;

            try
            {
                string? error = null;

                await Task.Run(() =>
                {
                    if (codec == CodecType.DSP)
                    {
                        error = LynEncoder.ConvertWavToDspSns(inputPath, outputPath, UpdateProgress, debugMode);
                    }
                    else
                    {
                        error = LynEncoder.ConvertWavToOggSns(inputPath, outputPath, UpdateProgress, debugMode);
                    }
                });

                if (string.IsNullOrEmpty(error))
                {
                    ShowResult(true, "SNS Successfully\nGenerated!");
                }
                else
                {
                    ShowResult(false, $"SNS Failed!\n\n{error}");
                }
            }
            catch (Exception ex)
            {
                ShowResult(false, $"SNS Failed!\n\n{ex.Message}");
            }
            finally
            {
                SetButtonsEnabled(true);
                StatusText.Text = "";
            }
        }

        private void SetButtonsEnabled(bool enabled)
        {
            NgcButton.IsEnabled = enabled;
            OggButton.IsEnabled = enabled;
            DebugCheckbox.IsEnabled = enabled;
        }

        private void ShowResult(bool success, string message)
        {
            // Set the rabbid image
            string imagePath;
            if (success)
            {
                imagePath = SuccessImage;
            }
            else
            {
                // Random failed image
                imagePath = _failedImages[_random.Next(_failedImages.Length)];
            }

            RabbidImage.Source = LoadImage(imagePath);

            // Set message
            ResultMessage.Text = message;

            // Show overlay
            ResultOverlay.Visibility = Visibility.Visible;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ResultOverlay.Visibility = Visibility.Collapsed;
        }

        private void CreditsButton_Click(object sender, RoutedEventArgs e)
        {
            CreditsOverlay.Visibility = Visibility.Visible;
        }

        private void CloseCredits_Click(object sender, RoutedEventArgs e)
        {
            CreditsOverlay.Visibility = Visibility.Collapsed;
        }

        private void CreditsOverlay_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            CreditsOverlay.Visibility = Visibility.Collapsed;
        }

        private void EggsLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.youtube.com/@EggsCantFly") { UseShellExecute = true });
        }

        private void SkibidiLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.youtube.com/@Skibidisigma-s5y") { UseShellExecute = true });
        }

        private void CruwbyLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.youtube.com/@Cruwby") { UseShellExecute = true });
        }
    }

    public enum CodecType
    {
        DSP,
        OGG
    }
}
