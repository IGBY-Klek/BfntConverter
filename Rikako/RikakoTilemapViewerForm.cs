using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace BfntConverterApp.Rikako
{
    internal partial class RikakoTilemapViewerForm : Form
    {
        private const int TilesPerRow = 24;
        private const int TilesPerSection = 5;
        private const int TileSize = 16;

        private readonly Dictionary<string, Rectangle> tilePositions = new();
        private readonly List<string[]> parsedRows = new();
        private Bitmap? sourceImage;
        private int currentSection;
        private int totalSections;
        private RikakoTilePreviewForm? tilePreviewForm;

        public RikakoTilemapViewerForm(Bitmap mpnTileSheet)
        {
            InitializeComponent();
            TrySetAppIcon();
            MainMenuStrip = menuStrip1;
            richTextBox1.TextChanged += RichTextBox1_TextChanged;
            SetTileSheet(mpnTileSheet);
        }

        public void SetTileSheet(Bitmap mpnTileSheet)
        {
            sourceImage?.Dispose();
            sourceImage = new Bitmap(mpnTileSheet);
            InitializeTilePositions();
            ShowTilePreview(sourceImage);
            GenerateFromText();
        }

        private void InitializeTilePositions()
        {
            if (sourceImage == null) return;

            tilePositions.Clear();
            var tilesPerRow = sourceImage.Width / TileSize;
            var rows = sourceImage.Height / TileSize;

            for (var y = 0; y < rows; y++)
            {
                for (var x = 0; x < tilesPerRow; x++)
                {
                    var hexValue = $"{y:X1}{x:X1}";
                    var tileRect = new Rectangle(x * TileSize, y * TileSize, TileSize, TileSize);
                    tilePositions[hexValue] = tileRect;
                }
            }
        }

        private void GenerateFromText(bool showWarnings = false)
        {
            if (string.IsNullOrWhiteSpace(richTextBox1.Text))
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
            parsedRows.AddRange(ParseInput(richTextBox1.Text));
            if (parsedRows.Count == 0)
            {
                ClearPreview();
                totalSections = 0;
                currentSection = 0;
                UpdateSectionLabel();
                if (showWarnings)
                {
                    MessageBox.Show(
                        "没有找到地图行。请输入类似 'Row { 0x00, 0x01, ... }' 的内容。",
                        "未检测到地图行",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
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

            using var resultImage = new Bitmap(TilesPerRow * TileSize, TilesPerSection * TileSize);
            using var g = Graphics.FromImage(resultImage);
            g.Clear(Color.White);

            var startRow = currentSection * TilesPerSection;
            var endRow = Math.Min(startRow + TilesPerSection, parsedRows.Count);

            for (var rowIndex = startRow; rowIndex < endRow; rowIndex++)
            {
                var hexValues = parsedRows[rowIndex];
                for (var i = 0; i < Math.Min(hexValues.Length, TilesPerRow); i++)
                {
                    var hex = hexValues[i].Trim().ToUpperInvariant().PadLeft(2, '0');
                    if (!tilePositions.TryGetValue(hex, out var sourceRect))
                    {
                        continue;
                    }

                    var destRect = new Rectangle(i * TileSize, (rowIndex - startRow) * TileSize, TileSize, TileSize);
                    g.DrawImage(sourceImage, destRect, sourceRect, GraphicsUnit.Pixel);
                }
            }

            pictureBox2.Image?.Dispose();
            pictureBox2.Image = new Bitmap(resultImage);
            pictureBox2.SizeMode = PictureBoxSizeMode.Zoom;
        }

        private void ClearPreview()
        {
            pictureBox2.Image?.Dispose();
            pictureBox2.Image = null;
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

        private void LoadMap_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Text Files|*.txt|所有文件 (*.*)|*.*"
            };
            if (ofd.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            richTextBox1.Text = File.ReadAllText(ofd.FileName);
            GenerateFromText(showWarnings: true);
        }

        private void Exit_Click(object? sender, EventArgs e) => Close();

        private void About_Click(object? sender, EventArgs e)
        {
            MessageBox.Show(
                "Rikako 地图块查看器\n使用 BFNT Converter 当前打开的 MPN 点阵图。",
                "关于",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void UpdateSectionLabel()
        {
            lblSectionNumber.Text = totalSections > 0
                ? $"Section: {currentSection + 1}/{totalSections}"
                : "Section: 0/0";
            btnPrevSection.Enabled = currentSection > 0;
            btnNextSection.Enabled = currentSection < totalSections - 1;
        }

        private void NextSection_Click(object? sender, EventArgs e)
        {
            if (currentSection >= totalSections - 1)
            {
                return;
            }

            currentSection++;
            UpdatePreview();
            UpdateSectionLabel();
        }

        private void PreviousSection_Click(object? sender, EventArgs e)
        {
            if (currentSection <= 0)
            {
                return;
            }

            currentSection--;
            UpdatePreview();
            UpdateSectionLabel();
        }

        private void RichTextBox1_TextChanged(object? sender, EventArgs e) => GenerateFromText();

        private void ShowTilePreview(Bitmap image)
        {
            tilePreviewForm?.Close();
            tilePreviewForm?.Dispose();

            tilePreviewForm = new RikakoTilePreviewForm();
            if (Icon != null)
            {
                tilePreviewForm.Icon = Icon;
            }

            tilePreviewForm.SetImage(image);
            tilePreviewForm.Show(this);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            tilePreviewForm?.Close();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                sourceImage?.Dispose();
                tilePreviewForm?.Dispose();
            }

            base.Dispose(disposing);
        }

        private void TrySetAppIcon()
        {
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            }
            catch
            {
                // Ignore icon failures to avoid crashing the UI
            }
        }
    }
}
