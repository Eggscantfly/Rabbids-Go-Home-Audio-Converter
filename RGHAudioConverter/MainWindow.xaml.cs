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
            
            // load font
            LoadCustomFont();
            
            // load images
            LoadImages();
            
            // init 4ch checkbox
            UpdateFourChannelCheckboxState();
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
                // load logo
                string logoPath = Path.Combine(_baseDir, "Contents", "Gui", "RGH logo", "RGHAC.png");
                if (File.Exists(logoPath))
                    LogoImage.Source = LoadBitmapImage(logoPath);

                // NGC button
                string ngcPath = Path.Combine(_baseDir, "Contents", "Gui", "NGC", "NGC.png");
                if (File.Exists(ngcPath))
                    NgcImage.Source = LoadBitmapImage(ngcPath);

                // OGG button
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
        private bool IsNormalize => NormalizeCheckbox.IsChecked == true;
        private bool IsFourChannel => FourChannelCheckbox.IsChecked == true && FourChannelCheckbox.IsEnabled;

        private int GetSelectedSampleRate()
        {
            if (SampleRateCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            {
                if (int.TryParse(item.Content.ToString(), out int rate))
                    return rate;
            }
            return 32000;
        }

        private OutputFormat GetSelectedFormat()
        {
            return FormatCombo.SelectedIndex == 1 ? OutputFormat.SON : OutputFormat.SNS;
        }

        private string GetFileExtension()
        {
            return GetSelectedFormat() == OutputFormat.SON ? ".son" : ".sns";
        }

        private ExtrasOption GetSelectedExtras()
        {
            return ExtrasCombo.SelectedIndex switch
            {
                1 => ExtrasOption.JustDance,
                2 => ExtrasOption.CustomBeats,
                _ => ExtrasOption.None
            };
        }

        private void FormatCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateFourChannelCheckboxState();
        }

        private void UpdateFourChannelCheckboxState()
        {
            if (FourChannelCheckbox != null)
            {
                // 4ch only for SON
                bool isRabbidsLand = FormatCombo.SelectedIndex == 1;
                FourChannelCheckbox.IsEnabled = isRabbidsLand;
                
                // uncheck if disabled
                if (!isRabbidsLand)
                {
                    FourChannelCheckbox.IsChecked = false;
                }
            }
        }

        private async Task ConvertAudio(CodecType codec)
        {
            var openDialog = new OpenFileDialog
            {
                Title = "Select WAV File",
                Filter = "WAV files (*.wav)|*.wav|All files (*.*)|*.*"
            };

            if (openDialog.ShowDialog() != true)
                return;

            string inputPath = openDialog.FileName;

            if (!LynEncoder.ValidateWavFile(inputPath, out string validationError))
            {
                ShowResult(false, validationError);
                return;
            }

            string ext = GetFileExtension();
            string formatName = GetSelectedFormat() == OutputFormat.SON ? "SON" : "SNS";

            var saveDialog = new SaveFileDialog
            {
                Title = $"Save {formatName} File",
                Filter = $"{formatName} files (*{ext})|*{ext}|All files (*.*)|*.*",
                FileName = Path.GetFileNameWithoutExtension(inputPath) + ext
            };

            if (saveDialog.ShowDialog() != true)
                return;

            string outputPath = saveDialog.FileName;

            SetButtonsEnabled(false);
            StatusText.Text = "Encoding... 0%";

            bool debugMode = IsDebugMode;
            bool normalize = IsNormalize;
            bool fourChannel = IsFourChannel;
            int targetSampleRate = GetSelectedSampleRate();
            bool forceMono = MonoCheckbox.IsChecked == true;
            OutputFormat format = GetSelectedFormat();
            ExtrasOption extras = GetSelectedExtras();

            try
            {
                string? error = null;

                await Task.Run(() =>
                {
                    if (codec == CodecType.DSP)
                    {
                        error = LynEncoder.ConvertWavToDspSns(inputPath, outputPath, UpdateProgress, debugMode, targetSampleRate, forceMono, format, normalize, fourChannel, extras);
                    }
                    else
                    {
                        error = LynEncoder.ConvertWavToOggSns(inputPath, outputPath, UpdateProgress, debugMode, targetSampleRate, forceMono, format, normalize, extras);
                    }
                });

                if (string.IsNullOrEmpty(error))
                {
                    ShowResult(true, $"{formatName} Successfully\nGenerated!");
                }
                else
                {
                    ShowResult(false, $"{formatName} Failed!\n\n{error}");
                }
            }
            catch (Exception ex)
            {
                ShowResult(false, $"{formatName} Failed!\n\n{ex.Message}");
            }
            finally
            {
                SetButtonsEnabled(true);
                StatusText.Text = "";
                
                // clear beat stealer after conversion
                if (LynEncoder.HasCustomBeats)
                {
                    LynEncoder.ClearCustomBeats();
                    Dispatcher.Invoke(() =>
                    {
                        BeatStealerPath.Text = "No file selected";
                        BeatStealerPath.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
                        BeatStealerStatus.Text = "";
                        if (ExtrasCombo.SelectedIndex == 2)
                        {
                            ExtrasCombo.SelectedIndex = 0;
                        }
                    });
                }
            }
        }

        private void SetButtonsEnabled(bool enabled)
        {
            NgcButton.IsEnabled = enabled;
            OggButton.IsEnabled = enabled;
            OptionsButton.IsEnabled = enabled;
        }

        private void ShowResult(bool success, string message)
        {
            // set rabbid image
            string imagePath;
            if (success)
            {
                imagePath = SuccessImage;
            }
            else
            {
                // random fail image
                imagePath = _failedImages[_random.Next(_failedImages.Length)];
            }

            RabbidImage.Source = LoadImage(imagePath);

            // set message
            ResultMessage.Text = message;

            // show overlay
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

        private void OptionsButton_Click(object sender, RoutedEventArgs e)
        {
            OptionsOverlay.Visibility = Visibility.Visible;
        }

        private void CloseOptions_Click(object sender, RoutedEventArgs e)
        {
            OptionsOverlay.Visibility = Visibility.Collapsed;
        }

        private void OptionsOverlay_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OptionsOverlay.Visibility = Visibility.Collapsed;
        }

        private void BeatStealerBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "SNS Files (*.sns)|*.sns|All Files (*.*)|*.*",
                Title = "Select Original SNS File to Extract Beats From"
            };

            if (dialog.ShowDialog() == true)
            {
                int beatCount = LynEncoder.ExtractBeatsFromSns(dialog.FileName);
                
                if (beatCount > 0)
                {
                    BeatStealerPath.Text = System.IO.Path.GetFileName(dialog.FileName);
                    BeatStealerPath.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
                    BeatStealerStatus.Text = $"✓ Beat labels found: {beatCount} beats";
                    BeatStealerStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)); // green
                    
                    // auto-select custom beats
                    ExtrasCombo.SelectedIndex = 2;
                }
                else
                {
                    BeatStealerPath.Text = System.IO.Path.GetFileName(dialog.FileName);
                    BeatStealerPath.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
                    BeatStealerStatus.Text = "✗ No beat labels found";
                    BeatStealerStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54)); // red
                }
            }
        }

        private void BeatStealerClear_Click(object sender, RoutedEventArgs e)
        {
            LynEncoder.ClearCustomBeats();
            BeatStealerPath.Text = "No file selected";
            BeatStealerPath.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
            BeatStealerStatus.Text = "";
            
            // reset if custom beats was selected
            if (ExtrasCombo.SelectedIndex == 2)
            {
                ExtrasCombo.SelectedIndex = 0;
            }
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
