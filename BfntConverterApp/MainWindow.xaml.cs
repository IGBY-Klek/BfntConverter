using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Pronama.ImageSharp.Formats.Bfnt;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Pbm;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using Configuration = SixLabors.ImageSharp.Configuration;
using Image = SixLabors.ImageSharp.Image;

namespace BfntConverterApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        internal class ViewModel : BindableBase
        {
            public string StatusText
            {
                get => _statusText;
                set => SetProperty(ref _statusText, value);
            }
            private string _statusText = "";

            public ObservableCollection<PaletteColor> PaletteColors { get; } = new();

            public bool HasPalette
            {
                get => _hasPalette;
                set => SetProperty(ref _hasPalette, value);
            }
            private bool _hasPalette;
        }

        internal class PaletteColor
        {
            public PaletteColor(int index, Rgb24 color, bool isTransparent)
            {
                Index = index;
                Brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
                Brush.Freeze();
                TextBrush = new SolidColorBrush(GetLuminance(color) > 140 ? Colors.Black : Colors.White);
                TextBrush.Freeze();
                ToolTip = $"#{index:000}: RGB({color.R}, {color.G}, {color.B}) / #{color.R:X2}{color.G:X2}{color.B:X2}" + (isTransparent ? " (透明)" : "");
            }

            public int Index { get; }
            public SolidColorBrush Brush { get; }
            public SolidColorBrush TextBrush { get; }
            public string ToolTip { get; }

            private static double GetLuminance(Rgb24 color) =>
                (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B);
        }

        private readonly ViewModel _viewModel = new();
        private Image<Bgra32>? _image;
        private string? _filePath;
        private BfntMetadata? _bfntMetadata;
        private PaletteWindow? _paletteWindow;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;
            SetTitle();


        }

        public MainWindow(IReadOnlyList<string?> args) : this()
        {
            if (args.Count > 0 && System.IO.File.Exists(args[0]))
                Open(args[0]);
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            ApplyBackgroundEffect();
        }

        private void ApplyBackgroundEffect()
        {
            var windowHandle = new WindowInteropHelper(this).Handle;

            WPFUI.Theme.Manager.Switch(WPFUI.Theme.Style.Dark);
            Background = Brushes.Transparent;
            WPFUI.Background.Manager.Apply(WPFUI.Background.BackgroundType.Mica, windowHandle);
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
                return;

            Open(files[0]);
        }

        private void MenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem item) return;

            var tag = item.Tag as string;
            switch (tag)
            {
                case "close":
                    Close();
                    break;
                case "help":
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://pronama.jp/bfnt-converter",
                        UseShellExecute = true
                    });
                    break;
                case "paletteViewer":
                    ShowPaletteViewer();
                    break;
            }
        }


        private void SetTitle(string? file = null)
        {
            var title = file == null ?
                "BFNT Converter" :
                $"BFNT Converter - {System.IO.Path.GetFileName(file)}";

            TitleBar.Title = title;
            Title = title;
        }

        private void Open(string? file)
        {
            // clear
            SetTitle();
            _viewModel.StatusText = "";
            ZoomImage.Source = null;
            ClearPaletteViewer();

            try
            {
                if (IsCdgFile(file))
                {
                    OpenCdg(file);
                    return;
                }

                var configuration = new Configuration(
                    new PngConfigurationModule(),
                    new JpegConfigurationModule(),
                    new GifConfigurationModule(),
                    new BmpConfigurationModule(),
                    new PbmConfigurationModule(),
                    new TgaConfigurationModule(),
                    new TiffConfigurationModule(),
                    new WebpConfigurationModule(),
                    new BfntConfigurationModule());

                // Workaround: Image.Load の format の Metadata は使えないので、Identify の format を使用
                string? formatName;
                {
                    var imageInfo = Image.Identify(configuration, file, out var format);
                    if (imageInfo == null)
                        return;

                    formatName = format.Name;
                    if (formatName == "BFNT")
                    {
                        _bfntMetadata = imageInfo.Metadata.GetBfntMetadata();
                        _viewModel.StatusText = $"{_bfntMetadata.ColorCount:#,0}色 ({_bfntMetadata.ColorBits + 1}bit)   {_bfntMetadata.Xdots}x{_bfntMetadata.Ydots}   {_bfntMetadata.Start}-{_bfntMetadata.End}   调色板数据{(_bfntMetadata.HasPalette ? "存在" : "不存在")}";
                    }
                    else if (formatName == "PI")
                    {
                        var piMetadata = imageInfo.Metadata.GetPiMetadata();
                        _bfntMetadata = null;
                        _viewModel.StatusText = $"PI   {imageInfo.Width}x{imageInfo.Height}   {piMetadata.ColorCount:#,0}色 ({piMetadata.NormalizedBitDepth}bit)   {piMetadata.CompressorModel}";
                    }
                    else
                    {
                        _bfntMetadata = null;
                        _viewModel.StatusText = $"{imageInfo.Width}x{imageInfo.Height}";
                    }
                }

                var image = Image.Load<Bgra32>(configuration, file, out _);
                if (image == null)
                    return;

                _image = image;
                SetImage(image);
                UpdatePaletteViewer(image, formatName);

                SetTitle(file);
                _filePath = file;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private static bool IsCdgFile(string? file)
        {
            var extension = System.IO.Path.GetExtension(file);
            return string.Equals(extension, ".cdg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".cd2", StringComparison.OrdinalIgnoreCase);
        }

        private void OpenCdg(string cdgPath)
        {
            var palettePath = SelectCdgPaletteFile(cdgPath);
            if (palettePath == null)
            {
                _viewModel.StatusText = "CDG/CD2 需要 RGB 或 PAL 调色板。";
                return;
            }

            var image = CdgDecoder.Decode(cdgPath, palettePath, out var info);
            _image = image;
            SetImage(image);
            SetPaletteViewer(info.Palette);

            _bfntMetadata = null;
            _filePath = cdgPath;
            SetTitle(cdgPath);

            var colorText = info.HasColorPlanes ? "16色 (4bit)" : "Alpha-only";
            var alphaText = info.HasAlphaPlane ? " + Alpha" : "";
            var imageCountText = info.Images > 1 ? $"   images={info.Images}" : "";
            var trailingByteText = info.TrailingByteCount > 0 ? $"   忽略尾随字节: {info.TrailingByteCount:#,0}" : "";
            _viewModel.StatusText = $"CDG/CD2(ZUN)   {info.Width}x{info.Height * info.Images}   {colorText}{alphaText}   unknown2=0x{info.Unknown2:X4}   plane_size={info.PlaneSize:#,0}   planes={info.Planes}{imageCountText}{trailingByteText}   调色板: {System.IO.Path.GetFileName(palettePath)}";
        }

        private string? SelectCdgPaletteFile(string cdgPath)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "请选择 CDG/CD2 的 RGB/PAL 调色板",
                Filter = "PC-98 调色板 (*.rgb; *.pal)|*.rgb;*.pal|RGB 调色板 (*.rgb)|*.rgb|PAL 调色板 (*.pal)|*.pal|所有文件 (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false,
                InitialDirectory = System.IO.Path.GetDirectoryName(cdgPath)
            };

            return openFileDialog.ShowDialog(this) == true ? openFileDialog.FileName : null;
        }

        private void SetImage(Image<Bgra32> image)
        {
            // Image to BitmapSource
            var pixelBytes = new byte[image.Width * image.Height * Unsafe.SizeOf<Bgra32>()];
            image.CopyPixelDataTo(pixelBytes);
            var bmp = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null,
                pixelBytes, image.Width * 4);

            ZoomBorder.Reset();
            ZoomImage.Source = bmp;
            ZoomCanvas.Width = bmp.Width;
            ZoomCanvas.Height = bmp.Height;
        }

        private void UpdatePaletteViewer(Image<Bgra32> image, string? formatName)
        {
            if (formatName == "BFNT")
            {
                _bfntMetadata = image.Metadata.GetBfntMetadata();
                SetPaletteViewer(_bfntMetadata.Palette, _bfntMetadata.TransparentPallets);
            }
            else
            {
                ClearPaletteViewer();
            }
        }

        private void SetPaletteViewer(IReadOnlyList<Rgb24> palette, IReadOnlySet<int>? transparentPaletteIndexes = null)
        {
            _viewModel.PaletteColors.Clear();
            for (var i = 0; i < palette.Count; i++)
            {
                _viewModel.PaletteColors.Add(new PaletteColor(i, palette[i], transparentPaletteIndexes?.Contains(i) ?? false));
            }

            _viewModel.HasPalette = palette.Count > 0;
        }

        private void ClearPaletteViewer()
        {
            _viewModel.PaletteColors.Clear();
            _viewModel.HasPalette = false;
        }


        private void ShowPaletteViewer()
        {
            if (!_viewModel.HasPalette)
            {
                MessageBox.Show("当前图像没有可显示的调色板。", "调色板查看器", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_paletteWindow != null)
            {
                _paletteWindow.Activate();
                return;
            }

            _paletteWindow = new PaletteWindow(_viewModel.PaletteColors)
            {
                Owner = this
            };
            _paletteWindow.Closed += (_, _) => _paletteWindow = null;
            _paletteWindow.Show();
        }

        private void Open(object sender, ExecutedRoutedEventArgs e) => OpenFileDialog();

        private void OpenFileDialog()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "BFNT (*.BFT; *.FNT)|*.BFT;*.FNT|PIXEL IMAGE (*.PI)|*.pi|CDG/CD2(ZUN) (*.cdg; *.cd2)|*.cdg;*.cd2|图像文件 (*.png; *.jpg; *.jpeg; *.jpe; *.jfif; *.exif; *.bmp; *.dib; *.rle; *.tiff; *.tif; *.gif; *.webp; *.pi)|*.png;*.jpg;*.jpeg;*.jpe;*.jfif;*.exif;*.bmp;*.dib;*.rle;*.tiff;*.tif;*.gif;*.webp;*.pi|所有文件 (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            Open(openFileDialog.FileName);
        }

        private void Save(object sender, ExecutedRoutedEventArgs e) => SaveFileDialog();

        private void SaveFileDialog()
        {
            if (_image == null) return;

            var saveWindow = new SaveWindow(_image, _filePath, _bfntMetadata)
            {
                Owner = this
            };
            saveWindow.ShowDialog();
        }

        private void Copy(object sender, ExecutedRoutedEventArgs e) => CopyImageToClipboard();
        private void CopyImageToClipboard()
        {
            if (ZoomImage.Source == null)
                return;

            IDataObject data = new DataObject();
            data.SetData(DataFormats.Bitmap, ZoomImage.Source, true);
            Clipboard.SetDataObject(data, true);
        }

        private void CanSave(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _image != null;
        }

        private void CanCopy(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _image != null;
        }

        private void Paste(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                var bitmapSource = Clipboard.GetImage();
                if (bitmapSource == null)
                {
                    return;
                }
                var pixels = new byte[(int)bitmapSource.Width * (int)bitmapSource.Height * 4];

                // BitmapSource から配列にコピー
                var stride = ((int)bitmapSource.Width * bitmapSource.Format.BitsPerPixel + 7) / 8;
                bitmapSource.CopyPixels(pixels, stride, 0);
                var image = Image.LoadPixelData<Bgra32>(pixels, (int)bitmapSource.Width, (int)bitmapSource.Height);


                _viewModel.StatusText = $"{(int)bitmapSource.Width}x{(int)bitmapSource.Height}";

                _image = image;
                SetImage(image);
                ClearPaletteViewer();

                SetTitle();
                _filePath = null;
                _bfntMetadata = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(messageBoxText: ex.Message);
            }

        }
    }
}
