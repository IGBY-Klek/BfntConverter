using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;

namespace BfntConverterApp
{
    /// <summary>
    /// PaletteWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class PaletteWindow : Window
    {
        internal class ViewModel : BindableBase
        {
            public ViewModel(ObservableCollection<MainWindow.PaletteColor> paletteColors)
            {
                PaletteColors = paletteColors;
                PaletteColors.CollectionChanged += PaletteColorsOnCollectionChanged;
                UpdatePaletteCountText();
            }

            public ObservableCollection<MainWindow.PaletteColor> PaletteColors { get; }

            public string PaletteCountText
            {
                get => _paletteCountText;
                set => SetProperty(ref _paletteCountText, value);
            }
            private string _paletteCountText = "";

            private void PaletteColorsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
                UpdatePaletteCountText();

            private void UpdatePaletteCountText() =>
                PaletteCountText = $"{PaletteColors.Count:#,0} 色";

            public void Detach() =>
                PaletteColors.CollectionChanged -= PaletteColorsOnCollectionChanged;
        }

        private readonly ViewModel _viewModel;

        internal PaletteWindow(ObservableCollection<MainWindow.PaletteColor> paletteColors)
        {
            InitializeComponent();
            _viewModel = new ViewModel(paletteColors);
            DataContext = _viewModel;
        }

        protected override void OnClosed(System.EventArgs e)
        {
            _viewModel.Detach();
            base.OnClosed(e);
        }
    }
}
