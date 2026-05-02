using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Microsoft.Win32;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Directory = System.IO.Directory;
using File = TagLib.File;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaFontFamily = System.Windows.Media.FontFamily;
using Point = System.Windows.Point;
namespace MEMENTO
{
    public partial class MainWindow : Window
    {
        private List<PhotoInfo> _allPhotos = new();
        private List<PhotoInfo> _filteredPhotos = new();
        private PhotoInfo? _currentPhoto;
        private BitmapImage? _currentFullImage;
        private ExifData _exif = new();
        private int _ratingFilter = 0;
        private string _selectedFont = "Segoe UI";
        private string _customFontName = "";
        public MainWindow()
        {
            InitializeComponent();

            if (PhotoListBox == null) PhotoListBox = (ListBox)FindName("PhotoListBox");
            if (RatingFilterCombo == null) RatingFilterCombo = (ComboBox)FindName("RatingFilterCombo");
            if (FontComboBox != null)
                FontComboBox.SelectionChanged += FontComboBox_SelectionChanged;
            ArtistTextBox.TextChanged += (s, e) => RefreshPreview();
            BorderThicknessSlider.ValueChanged += (s, e) => RefreshPreview();
            ShowLogoCheckBox.Checked += (s, e) => RefreshPreview();
            ShowLogoCheckBox.Unchecked += (s, e) => RefreshPreview();
            ShowExifCheckBox.Checked += (s, e) => RefreshPreview();
            ShowExifCheckBox.Unchecked += (s, e) => RefreshPreview();
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            _allPhotos.Clear();
            _filteredPhotos.Clear();
            _currentPhoto = null;
            _currentFullImage = null;
            GC.Collect();

            var dialog = new OpenFolderDialog { Title = "选择照片文件夹" };
            if (dialog.ShowDialog() == true)
            {
                LoadPhotos(dialog.FolderName);
            }
        }

        private void RatingFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            _ratingFilter = RatingFilterCombo.SelectedIndex;
            ApplyRatingFilter();
        }

        private void RefreshPreview_Click(object sender, RoutedEventArgs e) => RefreshPreview();
        private void FontComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontComboBox.SelectedItem is ComboBoxItem selected)
            {
                _selectedFont = selected.Content.ToString();
                RefreshPreview();
            }
        }
        private async void SaveImage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPhoto == null)
            {
                MessageBox.Show("请先选择照片");
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "JPEG Image|*.jpg",
                FileName = Path.GetFileNameWithoutExtension(_currentPhoto.Path) + "_framed.jpg"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var full = new BitmapImage();
                    full.BeginInit();
                    full.UriSource = new Uri(_currentPhoto.Path);
                    full.CacheOption = BitmapCacheOption.OnLoad;
                    full.EndInit();
                    full.Freeze();

                    var framedImage = CreateFramedImage(full, ArtistTextBox.Text, (int)BorderThicknessSlider.Value,
                        ShowLogoCheckBox.IsChecked == true, ShowExifCheckBox.IsChecked == true);

                    if (framedImage == null) return;

                    using var fs = new FileStream(dlg.FileName, FileMode.Create);
                    var encoder = new JpegBitmapEncoder();
                    encoder.QualityLevel = 92;
                    encoder.Frames.Add(BitmapFrame.Create(framedImage));
                    encoder.Save(fs);

                    await Dispatcher.InvokeAsync(() => MessageBox.Show("保存成功"));
                }
                finally
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
            }
        }

        private void OpenMarkable_Click(object sender, RoutedEventArgs e)
        {
            string markablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MARKABLE", "MARKABLE.exe");
            if (!System.IO.File.Exists(markablePath))
            {
                var dlg = new OpenFileDialog { Filter = "可执行文件|*.exe", Title = "定位 MARKABLE.exe" };
                if (dlg.ShowDialog() == true)
                    markablePath = dlg.FileName;
                else
                {
                    MessageBox.Show("未找到 MARKABLE.exe");
                    return;
                }
            }

            try
            {
                System.Diagnostics.Process.Start(markablePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动失败: {ex.Message}");
            }
        }

        private async void LoadPhotos(string folder)
        {
            var exts = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            var files = Directory.GetFiles(folder)
                .Where(f => exts.Contains(Path.GetExtension(f).ToLower()))
                .Select(f => new PhotoInfo
                {
                    Path = f,
                    Name = Path.GetFileName(f),
                    Rating = GetRatingFromFile(f)
                }).ToList();

            _allPhotos = files;
            ApplyRatingFilter();

            await GenerateThumbnailsAsync(_filteredPhotos);
        }

        private async Task GenerateThumbnailsAsync(List<PhotoInfo> photos)
        {
            var semaphore = new SemaphoreSlim(4);
            var tasks = photos.Select(async photo =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var thumb = await Task.Run(() => GetThumbnailSync(photo.Path));
                    await Dispatcher.InvokeAsync(() => photo.Thumbnail = thumb);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        private BitmapImage? GetThumbnailSync(string path)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path);
                bmp.DecodePixelWidth = 80;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private int GetRatingFromFile(string path)
        {
            try
            {
                using var tag = File.Create(path);
                var img = tag as TagLib.Image.File;
                return (int)(img?.ImageTag?.Rating ?? 0);
            }
            catch
            {
                return 0;
            }
        }

        private void ApplyRatingFilter()
        {
            if (PhotoListBox == null) return;
            _filteredPhotos = _allPhotos.Where(p => p.Rating >= _ratingFilter).ToList();
            PhotoListBox.ItemsSource = null;
            PhotoListBox.ItemsSource = _filteredPhotos;
            if (_filteredPhotos.Any())
                PhotoListBox.SelectedIndex = 0;
        }

        private void PhotoListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PhotoListBox.SelectedItem is not PhotoInfo photo) return;

            _currentFullImage = null;
            _currentPhoto = photo;
            LoadExif(photo.Path);
            LoadFullImage(photo.Path);
            RefreshPreview();
        }

        private void LoadFullImage(string path)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path);
            bmp.DecodePixelWidth = 800;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            _currentFullImage = bmp;
        }

        private void LoadExif(string path)
        {
            try
            {
                var dirs = ImageMetadataReader.ReadMetadata(path);
                var ifd0 = dirs.OfType<ExifIfd0Directory>().FirstOrDefault();
                var exifSub = dirs.OfType<ExifSubIfdDirectory>().FirstOrDefault();

                _exif.Make = ifd0?.GetString(ExifDirectoryBase.TagMake)?.Trim() ?? "";
                _exif.Model = ifd0?.GetString(ExifDirectoryBase.TagModel)?.Trim() ?? "";
                _exif.Lens = exifSub?.GetString(ExifDirectoryBase.TagLensModel)?.Trim() ?? "";
                _exif.DateTime = ifd0?.GetString(ExifDirectoryBase.TagDateTime)?.Trim() ?? "";

                if (!string.IsNullOrEmpty(_exif.DateTime))
                {
                    if (DateTime.TryParseExact(_exif.DateTime, "yyyy:MM:dd HH:mm:ss",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                    {
                        _exif.DateTime = dt.ToString("'Year 'yyyy 'Month' MM");
                    }
                    else
                    {
                        _exif.DateTime = _exif.DateTime.Split(' ')[0].Replace(":", "/");
                    }
                }
                _exif.Artist = ifd0?.GetString(ExifDirectoryBase.TagArtist)?.Trim() ?? "";

                Dispatcher.Invoke(() =>
                {
                    CameraModelText.Text = $"{_exif.Make} {_exif.Model}".Trim();
                    LensModelText.Text = string.IsNullOrEmpty(_exif.Lens) ? "未记录" : _exif.Lens;
                    DateTimeText.Text = string.IsNullOrEmpty(_exif.DateTime) ? "未知" : _exif.DateTime;
                    ArtistTextBox.Text = _exif.Artist;
                });
            }
            catch
            {
                Dispatcher.Invoke(() =>
                {
                    CameraModelText.Text = "读取失败";
                    LensModelText.Text = "读取失败";
                    DateTimeText.Text = "读取失败";
                });
            }
        }

        private void RefreshPreview()
        {
            if (_currentFullImage == null) return;
            var framed = CreateFramedImage(_currentFullImage, ArtistTextBox.Text, (int)BorderThicknessSlider.Value,
                ShowLogoCheckBox.IsChecked == true, ShowExifCheckBox.IsChecked == true);

            if (framed != null)
            {
                PreviewImage.Source = framed;
            }
        }

        private BitmapImage? CreateFramedImage(BitmapImage source, string artist, int bottomHeight, bool showLogo, bool showExif)
        {
            if (source == null) return null;


            source.Freeze();

            int w = source.PixelWidth;
            int h = source.PixelHeight;

            // Output limtation - TO SAVE YOUR COMPUTER
            int targetWidth = Math.Min(w, 1800);
            double scale = (double)targetWidth / w;
            int targetHeight = (int)(h * scale);

            int infoHeight = bottomHeight;
            infoHeight = Math.Max(30, Math.Min(150, infoHeight));
            int totalW = targetWidth;
            int totalH = targetHeight + infoHeight;

            var renderBitmap = new RenderTargetBitmap(totalW, totalH, 96, 96, PixelFormats.Pbgra32);
            var dv = new DrawingVisual();

            using (var dc = dv.RenderOpen())
            {

                dc.DrawRectangle(MediaBrushes.White, null, new Rect(0, 0, totalW, totalH));

                var imageRect = new Rect(0, 0, targetWidth, targetHeight);
                dc.DrawImage(source, imageRect);

                int infoY = targetHeight;

                // 改为细体 Light
                var lightTypeface = new Typeface(new MediaFontFamily(_selectedFont), FontStyles.Normal, FontWeights.Light, FontStretches.Normal);
                var italicLightTypeface = new Typeface(new MediaFontFamily(_selectedFont), FontStyles.Italic, FontWeights.Light, FontStretches.Normal);

                var gray80 = new SolidColorBrush(MediaColor.FromRgb(80, 80, 80));
                var black = MediaBrushes.Black;
                var gray = MediaBrushes.Gray;
                var darkSlate = new SolidColorBrush(MediaColor.FromRgb(47, 79, 79));

                int padding = 20;
                int leftX = padding;
                int rightX = totalW - padding;

                int mainFontSize = Math.Max(14, Math.Min(32, (int)(totalW * 0.02)));
                int subFontSize = Math.Max(12, mainFontSize - 2);

                double dpiScale = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                double maxLeftWidth = totalW / 2 - padding * 2;

                double leftTotalHeight = 0;
                if (showLogo && !string.IsNullOrEmpty(_exif.Make + _exif.Model))
                {
                    string camera = $"{_exif.Make} {_exif.Model}".Trim();
                    var cameraText = new FormattedText(camera, CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, lightTypeface, mainFontSize, gray80, dpiScale);
                    leftTotalHeight += cameraText.Height;

                    if (showExif && !string.IsNullOrEmpty(_exif.Lens))
                    {
                        var lensText = new FormattedText(_exif.Lens, CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight, lightTypeface, subFontSize, gray, dpiScale);
                        leftTotalHeight += lensText.Height + 6;
                    }
                }


                double rightTotalHeight = 0;
                if (showExif && !string.IsNullOrEmpty(_exif.DateTime))
                {
                    var dateText = new FormattedText(_exif.DateTime, CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, lightTypeface, subFontSize, black, dpiScale);
                    rightTotalHeight += dateText.Height;
                }
                if (!string.IsNullOrEmpty(artist))
                {
                    var artistText = new FormattedText($"© {artist}", CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, italicLightTypeface, subFontSize, darkSlate, dpiScale);
                    rightTotalHeight += artistText.Height + 6;
                }


                double maxTextHeight = Math.Max(leftTotalHeight, rightTotalHeight);
                int startY = infoY + (int)((infoHeight - maxTextHeight) / 2);
                if (startY < infoY + 8) startY = infoY + 8;


                if (showLogo && !string.IsNullOrEmpty(_exif.Make + _exif.Model))
                {
                    string camera = $"{_exif.Make} {_exif.Model}".Trim();
                    var cameraText = new FormattedText(camera, CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, lightTypeface, mainFontSize, gray80, dpiScale);
                    if (cameraText.Width > maxLeftWidth)
                        cameraText = new FormattedText(camera, CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight, lightTypeface, mainFontSize - 2, gray80, dpiScale);
                    dc.DrawText(cameraText, new Point(leftX, startY));

                    double currentY = startY + cameraText.Height + 6;
                    if (showExif && !string.IsNullOrEmpty(_exif.Lens))
                    {
                        var lensText = new FormattedText(_exif.Lens, CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight, lightTypeface, subFontSize, gray, dpiScale);
                        if (lensText.Width > maxLeftWidth)
                            lensText = new FormattedText(_exif.Lens, CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight, lightTypeface, subFontSize - 2, gray, dpiScale);
                        dc.DrawText(lensText, new Point(leftX, currentY));
                    }
                }


                double rightStartY = startY;
                if (showExif && !string.IsNullOrEmpty(_exif.DateTime))
                {
                    var dateText = new FormattedText(_exif.DateTime, CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, lightTypeface, subFontSize, black, dpiScale);
                    dc.DrawText(dateText, new Point(rightX - dateText.Width, rightStartY));
                    rightStartY += dateText.Height + 6;
                }
                if (!string.IsNullOrEmpty(artist))
                {
                    var artistText = new FormattedText($"© {artist}", CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, italicLightTypeface, subFontSize, darkSlate, dpiScale);
                    dc.DrawText(artistText, new Point(rightX - artistText.Width, rightStartY));
                }
            }


            renderBitmap.Render(dv);
            renderBitmap.Freeze();

            var bitmapImage = new BitmapImage();
            using (var ms = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
                encoder.Save(ms);
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = ms;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
            }
            return bitmapImage;
        }

        public class PhotoInfo : INotifyPropertyChanged
        {
            private BitmapImage? _thumbnail;

            public string Path { get; set; } = "";
            public string Name { get; set; } = "";
            public int Rating { get; set; }
            public string RatingText => Rating > 0 ? $"⭐{Rating}" : "";

            public BitmapImage? Thumbnail
            {
                get => _thumbnail;
                set
                {
                    if (_thumbnail != value)
                    {
                        _thumbnail = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
                    }
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        public class ExifData
        {
            public string Make { get; set; } = "";
            public string Model { get; set; } = "";
            public string Lens { get; set; } = "";
            public string DateTime { get; set; } = "";
            public string Artist { get; set; } = "";
        }

    }
}