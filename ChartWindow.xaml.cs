using LiveChartsCore.SkiaSharpView;
using LiveChartsCore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using System.Collections.ObjectModel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView.WinUI;
using LiveChartsCore.SkiaSharpView.SKCharts;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace uart
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ChartWindow : Window
    {
        public ISeries[] series = [
            new LineSeries<int> {
                Values = new ObservableCollection<int> { },
                Fill = null,
                GeometrySize = 1
            }];
        public ICartesianAxis[] xAxes = [
            new Axis {
                Name = "time",
                UnitWidth = TimeSpan.FromMilliseconds(160).Ticks,
                MinLimit = 0,
                MaxLimit = 100,
            }];
        public ICartesianAxis[] yAxes = [
            new Axis {
                Name = "adcRaw",
            }];

        public ChartWindow()
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);
            this.AppWindow.Closing += (s, e) => lineChart.ContextFlyout.Hide();
        }

        private async void click_saveCBF(object sender, RoutedEventArgs e)
        {
            FileSavePicker savePicker = new();
            // Retrieve the window handle (HWND) of the current WinUI 3 window.
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            // Initialize the file picker with the window handle (HWND).
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);
            // Dropdown of file types the user can save the file as
            savePicker.FileTypeChoices.Add("line chart", new List<string>() { ".png" });
            // Open the picker for the user to pick a file
            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                // Prevent updates to the remote version of the file until we finish making changes and call CompleteUpdatesAsync.
                CachedFileManager.DeferUpdates(file);
                var skChart = new SKCartesianChart(lineChart);
                using var stream = await file.OpenStreamForWriteAsync();
                skChart.SaveImage(stream);
            }
            lineChart.ContextFlyout.Hide();
        }

        private void click_deleteCBF(object sender, RoutedEventArgs e)
        {
            (series[0].Values as ObservableCollection<int>).Clear();
            xAxes[0].MinLimit = 0;
            xAxes[0].MaxLimit = 100;
        }
    }
}
