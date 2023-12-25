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
        }
    }
}
