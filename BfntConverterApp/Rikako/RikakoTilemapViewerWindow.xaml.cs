using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace BfntConverterApp.Rikako
{
    public partial class RikakoTilemapViewerWindow : Window
    {
        private const int TilesPerRow = 24;
        private const int TilesPerSection = 5;
        private const int TileSize = 16;
        private const int BytesPerPixel = 4;

        private readonly Dictionary<string, (int X, int Y)> tilePositions = new();
        private readonly List<string[]> parsedRows = new();
        private Image<Bgra32>? sourceImage;
        private int currentSection;
        private int totalSections;

        public RikakoTilemapViewerWindow(Image<Bgra32> mpnTileSheet)
        {
            WPFUI.Theme.Manager.Switch(WPFUI.Theme.Style.Dark);
            InitializeComponent();
            SetTileSheet(mpnTileSheet);
        }

        public void SetTileSheet(Image<Bgra32> mpnTileSheet)
        {
            sourceImage?.Dispose();
            sourceImage = mpnTileSheet.Clone();
            InitializeTilePositions();
            GenerateFromText();
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            ApplyBackgroundEffect();
        }

        private void ApplyBackgroundEffect()
        {
            var windowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            Background = Brushes.Transparent;
            WPFUI.Background.Manager.Apply(WPFUI.Background.BackgroundType.Mica, windowHandle);
        }

        private void InitializeTilePositions()
        {
            if (sourceImage == null) return;

            tilePositions.Clear();
            var sourceTilesPerRow = sourceImage.Width / TileSize;
            var rows = sourceImage.Height / TileSize;

            for (var y = 0; y < rows; y++)
            {
                for (var x = 0; x < sourceTilesPerRow; x++)
                {
                    var hexValue = $"{y:X1}{x:X1}";
                    tilePositions[hexValue] = (x * TileSize, y * TileSize);
                }
            }
        }

        private void GenerateFromText(bool showWarnings = false)
        {
            if (string.IsNullOrWhiteSpace(MapTextBox.Text))
            {
                parsedRows.Clear();
                totalSections = 0;
                currentSection = 0;
                ClearPreview();
                UpdateSectionLabel();
                return;
            }

            if (sourceImage == null)
                return;

            parsedRows.Clear();
            parsedRows.AddRange(ParseInput(MapTextBox.Text));
            if (parsedRows.Count == 0)
            {
                ClearPreview();
                totalSections = 0;
                currentSection = 0;
                UpdateSectionLabel();
                if (showWarnings)
                {
                    MessageBox.Show(
                        this,
                        "没有找到地图行。请输入类似 'Row { 0x00, 0x01, ... }' 的内容。",
                        "未检测到地图行",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                return;
            }

            totalSections = (int)Math.Ceiling(parsedRows.Count / (double)TilesPerSection);
            currentSection = 0;
            UpdatePreview();
            UpdateSectionLabel();
        }

        private void UpdatePreview()
        {
            if (sourceImage == null || parsedRows.Count == 0) return;

            var sourcePixels = new byte[sourceImage.Width * sourceImage.Height * BytesPerPixel];
            sourceImage.CopyPixelDataTo(sourcePixels);

            var previewWidth = TilesPerRow * TileSize;
            var previewHeight = TilesPerSection * TileSize;
            var previewPixels = CreateWhitePixelBuffer(previewWidth, previewHeight);
            DrawCurrentSection(sourcePixels, sourceImage.Width, previewPixels, previewWidth);

            var bitmapSource = CreateBitmapSource(previewPixels, previewWidth, previewHeight);
            PreviewZoomBorder.Reset();
            PreviewImage.Source = bitmapSource;
            PreviewCanvas.Width = bitmapSource.Width;
            PreviewCanvas.Height = bitmapSource.Height;
        }

        private void DrawCurrentSection(
            byte[] sourcePixels,
            int sourceWidth,
            byte[] previewPixels,
            int previewWidth)
        {
            var startRow = currentSection * TilesPerSection;
            var endRow = Math.Min(startRow + TilesPerSection, parsedRows.Count);

            for (var rowIndex = startRow; rowIndex < endRow; rowIndex++)
            {
                var hexValues = parsedRows[rowIndex];
                for (var i = 0; i < Math.Min(hexValues.Length, TilesPerRow); i++)
                {
                    var hex = hexValues[i].Trim().ToUpperInvariant().PadLeft(2, '0');
                    if (!tilePositions.TryGetValue(hex, out var sourcePoint))
                    {
                        continue;
                    }

                    CopyTile(
                        sourcePixels,
                        sourceWidth,
                        sourcePoint.X,
                        sourcePoint.Y,
                        previewPixels,
                        previewWidth,
                        i * TileSize,
                        (rowIndex - startRow) * TileSize);
                }
            }
        }

        private static void CopyTile(
            byte[] sourcePixels,
            int sourceWidth,
            int sourceX,
            int sourceY,
            byte[] previewPixels,
            int previewWidth,
            int destinationX,
            int destinationY)
        {
            for (var y = 0; y < TileSize; y++)
            {
                var sourceOffset = (((sourceY + y) * sourceWidth) + sourceX) * BytesPerPixel;
                var destinationOffset = (((destinationY + y) * previewWidth) + destinationX) * BytesPerPixel;
                Buffer.BlockCopy(sourcePixels, sourceOffset, previewPixels, destinationOffset, TileSize * BytesPerPixel);
            }
        }

        private static byte[] CreateWhitePixelBuffer(int width, int height)
        {
            var pixels = new byte[width * height * BytesPerPixel];
            Array.Fill<byte>(pixels, 255);
            return pixels;
        }

        private void ClearPreview()
        {
            PreviewImage.Source = null;
            PreviewCanvas.Width = 0;
            PreviewCanvas.Height = 0;
        }

        private static List<string[]> ParseInput(string input)
        {
            var result = new List<string[]>();
            const string pattern = @"row\s*\{([^}]*)\}";
            var matches = Regex.Matches(input, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                if (match.Groups.Count <= 1)
                {
                    continue;
                }

                var rowContent = match.Groups[1].Value.Trim();
                var hexValues = Regex.Matches(rowContent, @"(?:0x|0X)?([0-9a-fA-F]{1,2})")
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value.ToUpperInvariant().PadLeft(2, '0'))
                    .ToArray();

                if (hexValues.Length > 0)
                {
                    result.Add(hexValues);
                }
            }

            return result;
        }

        private void LoadMap_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Text Files|*.txt|所有文件 (*.*)|*.*"
            };
            if (ofd.ShowDialog(this) != true)
            {
                return;
            }

            MapTextBox.Text = File.ReadAllText(ofd.FileName);
            GenerateFromText(showWarnings: true);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                this,
                "Rikako 地图块查看器\n使用 BFNT Converter 当前打开的 MPN 点阵图。",
                "关于",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void UpdateSectionLabel()
        {
            SectionNumberTextBlock.Text = totalSections > 0
                ? $"Section: {currentSection + 1}/{totalSections}"
                : "Section: 0/0";
            PreviousSectionButton.IsEnabled = currentSection > 0;
            NextSectionButton.IsEnabled = currentSection < totalSections - 1;
        }

        private void NextSection_Click(object sender, RoutedEventArgs e)
        {
            if (currentSection >= totalSections - 1)
            {
                return;
            }

            currentSection++;
            UpdatePreview();
            UpdateSectionLabel();
        }

        private void PreviousSection_Click(object sender, RoutedEventArgs e)
        {
            if (currentSection <= 0)
            {
                return;
            }

            currentSection--;
            UpdatePreview();
            UpdateSectionLabel();
        }

        private void MapTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => GenerateFromText();

        protected override void OnClosed(EventArgs e)
        {
            sourceImage?.Dispose();
            sourceImage = null;
            base.OnClosed(e);
        }

        private static BitmapSource CreateBitmapSource(byte[] pixelBytes, int width, int height)
        {
            var bitmapSource = BitmapSource.Create(
                width,
                height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                pixelBytes,
                width * BytesPerPixel);
            bitmapSource.Freeze();
            return bitmapSource;
        }
    }
}
