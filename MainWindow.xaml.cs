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
using Windows.Foundation.Collections;

using Windows.Devices.SerialCommunication;
using System.Collections.ObjectModel;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.System.Threading;
using LiveChartsCore.SkiaSharpView.SKCharts;
using LiveChartsCore.SkiaSharpView.WinUI;
using LiveChartsCore.Defaults;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace uart
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public const ushort row = 32;
        public const ushort col = 32;

        private StorageFolder folder;
        private DataReader reader;
        private SerialDevice com;
        private nint? handleBle = null;
        private InMemoryRandomAccessStream bleReadStream;
        //private readonly GloveModel model = new();
        //private readonly DispatcherQueueController thread_collect = DispatcherQueueController.CreateOnDedicatedThread();

        public ObservableCollection<DeviceInformation> comInfos = [];
        public ObservableCollection<DeviceInformation> bleInfos = [];
        internal ViewModel_switch viewModel_Switch = new();
        internal ObservableCollection<HeatMap_pixel> palm = [];
        internal ObservableCollection<HeatMap_pixel> f1 = [];
        internal ObservableCollection<HeatMap_pixel> f2 = [];
        internal ObservableCollection<HeatMap_pixel> f3 = [];
        internal ObservableCollection<HeatMap_pixel> f4 = [];
        internal ObservableCollection<ViewModel_lineChart> lineCharts = [];
        internal bool info_comOpen = true;
        internal bool info_bleOpen = true;
        private int lastPivotIndex;

        public MainWindow()
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);
            for (int i = 0; i < 7; i++)
                for (int j = 0; j < 8; j++)
                    if ((i == 3 && j == 7) || (i > 4 && j == 0) || (i > 3 && j > 4))
                        palm.Add(new HeatMap_pixel(Visibility.Collapsed));
                    else
                        palm.Add(new HeatMap_pixel(Visibility.Visible));
            for (int i = 0; i < 24; i++)
                for (int j = 0; j < 8; j++)
                {
                    f1.Add(new HeatMap_pixel(Visibility.Visible));
                    f2.Add(new HeatMap_pixel(Visibility.Visible));
                    f3.Add(new HeatMap_pixel(Visibility.Visible));
                    f4.Add(new HeatMap_pixel(Visibility.Visible));
                }

            lastPivotIndex = pivot.SelectedIndex;
        }

        private void heatMapValue2UI(ushort[,] heatmapValue)
        {
            for (int i = 0; i < row; i++)
                for (int j = 0; j < col; j++)
                    if (i < 24)
                        switch (j / 8)
                        {
                        case 0:
                            f1[i * 8 + j].adcValue = heatmapValue[i, j];
                            f1[i * 8 + j].chartLine?.chartUpdate(f1[i * 8 + j].adcValue);
                            break;
                        case 1:
                            f2[i * 8 + j - 8].adcValue = heatmapValue[i, j];
                            f2[i * 8 + j - 8].chartLine?.chartUpdate(f2[i * 8 + j - 8].adcValue);
                            break;
                        case 2:
                            f3[i * 8 + j - 16].adcValue = heatmapValue[i, j];
                            f3[i * 8 + j - 16].chartLine?.chartUpdate(f3[i * 8 + j - 16].adcValue);
                            break;
                        case 3:
                            f4[i * 8 + j - 24].adcValue = heatmapValue[i, j];
                            f4[i * 8 + j - 24].chartLine?.chartUpdate(f4[i * 8 + j - 24].adcValue);
                            break;
                        }
                    else if (i < 31 && j < 8)
                    {
                        palm[(i - 24) * 8 + j].adcValue = heatmapValue[i, j];
                        palm[(i - 24) * 8 + j].chartLine?.chartUpdate(palm[(i - 24) * 8 + j].adcValue);
                    }
        }

        private void click_splitViewPaneBtn(object sender, RoutedEventArgs e)
        {
            sv.IsPaneOpen = true;
        }

        private async void click_startBtn(object sender, RoutedEventArgs e)
        {
            var mode = lastPivotIndex;
            if (viewModel_Switch.isStartIcon)
            {
                switch (lastPivotIndex)
                {
                    case 0:
                        if (combobox_com.SelectedItem != null)
                        {
                            try
                            {
                                com = await SerialDevice.FromIdAsync((combobox_com.SelectedItem as DeviceInformation).Id);
                                if (com != null)
                                {
                                    com.BaudRate = Convert.ToUInt32(combobox_baud.SelectedValue);
                                    com.DataBits = Convert.ToUInt16(combobox_dataBits.SelectedValue);
                                    com.StopBits = (SerialStopBitCount)combobox_stopBits.SelectedIndex;
                                    com.Parity = (SerialParity)combobox_parity.SelectedIndex;
                                    com.ReadTimeout = TimeSpan.FromMilliseconds(40);
                                    reader = new(com.InputStream) { ByteOrder = ByteOrder.BigEndian };

                                    info_comOpen = false;
                                    info_com.IsOpen = false;
                                }
                            }
                            catch (Exception error)
                            {
                                info_comOpen = true;
                                info_com.IsOpen = true;
                                info_com.Message = error.ToString();
                                return;
                            }
                        }
                        break;

                    case 1:
                        if (handleBle == null)
                        {
                            info_ble.Message = "蓝牙未连接";
                            info_ble.IsOpen = true;
                            info_bleOpen = true;
                            return;
                        }
                        //这里转换需要注意，目前将停止位和校验位定死
                        var success = CH9140.CH9140UartSetSerialBaud((nint)handleBle, Convert.ToInt32(combobox_baud.SelectedValue), Convert.ToInt32(combobox_dataBits.SelectedValue), 1, 0);
                        bleReadStream.Size = 0;
                        bleReadStream.Seek(0);
                        reader = new(bleReadStream.GetInputStreamAt(0)) { ByteOrder = ByteOrder.BigEndian };
                        info_ble.IsOpen = false;
                        info_bleOpen = false;
                        break;

                    default:
                        return;
                }
                viewModel_Switch.isStartIcon = false;
                //thread_collect.DispatcherQueue.TryEnqueue(async () =>
                _ = ThreadPool.RunAsync(async (item) =>
                {
                    if (mode == 1)
                        System.Threading.Thread.Sleep(165);
                    // TODO: 串口读取部分代码更新，merge到其他分支
                    while (true)
                    {
                        if (viewModel_Switch.isStartIcon)
                        {
                            //thread_collect.ShutdownQueueAsync();
                            if (mode == 0)
                                com.Dispose();
                            reader.Dispose();
                            return;
                        }
                        while (true)
                        {
                            if (reader.UnconsumedBufferLength < 2)
                                await reader.LoadAsync(row * col * 2 + 2);
                            if (reader.ReadByte() == 0xff)
                                if (reader.ReadByte() == 0xff)
                                {
                                    if (reader.UnconsumedBufferLength < row * col * 2)
                                        await reader.LoadAsync(row * col * 2 - reader.UnconsumedBufferLength);
                                    break;
                                }
                        }
                        ushort[,] heatmapValue = new ushort[row, col];
                        for (int i = 0; i < row; i++)
                            for (int j = 0; j < col; j++)
                                heatmapValue[i, j] = reader.ReadUInt16();
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            if (folder != null)
                            {
                                string fileName = DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss.fff") + ".csv";
                                using var writer = new StreamWriter(System.IO.Path.Combine(folder.Path, fileName));
                                for (int i = 0; i < row; i++)
                                {
                                    for (int j = 0; j < col; j++)
                                        writer.Write(heatmapValue[i, j] + ",");
                                    writer.Write('\n');
                                }
                            }
                            heatMapValue2UI(heatmapValue);
                        });
                    }
                });
            }
            else if (!viewModel_Switch.isStartIcon)
            {
                viewModel_Switch.isStartIcon = true;
                info_com.Title = "串口错误";
                info_com.Severity = InfoBarSeverity.Error;
                info_com.IsOpen = false;
            }
        }

        private async void toggle_imageCollectSw(object sender, RoutedEventArgs e)
        {
            ToggleSwitch imageCollectSw = sender as ToggleSwitch;
            if (imageCollectSw.IsOn)
            {
                FolderPicker folderPicker = new();
                //var window = WindowHelper.GetWindowForElement(this);
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                // Initialize the folder picker with the window handle (HWND).
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hWnd);
                // Set options for your folder picker
                folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
                folderPicker.FileTypeFilter.Add("*");
                // Open the picker for the user to pick a folder
                folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", folder);
                    ts_imageCollect.OnContent = folder.Path;
                }
                else
                    imageCollectSw.IsOn = false;
            }
            else
                folder = null;
        }

        //created by bing
        private void pointerEntered_splitViewPaneBtn(object sender, PointerRoutedEventArgs e)
        {
            //// 创建一个Storyboard对象
            //Storyboard storyboard = new Storyboard();
            //// 创建一个DoubleAnimation对象，用于改变RotateTransform对象的Angle属性
            //DoubleAnimation animation = new DoubleAnimation();
            //// 设置动画的目标属性为Angle
            //Storyboard.SetTargetProperty(animation, "Angle");
            //// 设置动画的目标对象为RotateTransform对象
            //Storyboard.SetTarget(animation, splitViewPaneBtn_rotate);
            //// 设置动画的开始值为0
            //animation.From = 0;
            //// 设置动画的结束值为360
            //animation.To = 60;
            //// 设置动画的持续时间为1秒
            //animation.Duration = new Duration(TimeSpan.FromSeconds(0.2));
            //// 将动画添加到Storyboard对象中
            //storyboard.Children.Add(animation);
            //// 启动Storyboard对象
            //storyboard.Begin();
            icon_setting.Rotation = icon_setting.Rotation + 120 % 360;
        }

        private void selectionChanged_rangeCb(object sender, SelectionChangedEventArgs e)
        {
            ushort range = Convert.ToUInt16((sender as ComboBox).SelectedValue);
            HeatMap_pixelHelper.range = range;
            if (legendRange != null)
                legendRange.Text = range.ToString();
        }

        private void selectionChanged_pivot(object sender, SelectionChangedEventArgs e)
        {
            switch (pivot.SelectedIndex)
            {
            case 0:
                lastPivotIndex = 0;
                info_ble.IsOpen = false;
                if(info_comOpen)
                    info_com.IsOpen = true;
                break;
            case 1:
                lastPivotIndex = 1;
                info_com.IsOpen = false;
                if (info_bleOpen)
                    info_ble.IsOpen = true;
                break;
            }
        }

        private void toggled_bleConn(object sender, RoutedEventArgs e)
        {
            if ((sender as ToggleSwitch).IsOn)
                if (combobox_ble.SelectedItem != null)
                {
                    ((sender as ToggleSwitch).OnContent as ProgressRing).IsActive = true;
                    var id = (combobox_ble.SelectedItem as DeviceInformation).Id;
                    _ = ThreadPool.RunAsync((item) =>
                    {
                        bleReadStream = new();
                        handleBle = CH9140.CH9140UartOpenDevice(id, null, null, (p, buf, len) =>
                        {
                            byte[] data = new byte[len];
                            unsafe
                            {
                                //fixed (byte* source = buf)
                                fixed (byte* destin = data)
                                    System.Buffer.MemoryCopy((void*)buf, destin, (int)len, (int)len);
                            }
                            bleReadStream.WriteAsync(WindowsRuntimeBufferExtensions.AsBuffer(data, 0, (int)len));
                        });
                        this.DispatcherQueue.TryEnqueue(() => {
                            ((sender as ToggleSwitch).OnContent as ProgressRing).IsActive = false;
                            if (handleBle == null)
                                (sender as ToggleSwitch).IsOn = false;
                        });
                    });
                }
                else
                    (sender as ToggleSwitch).IsOn = false;
            else if (handleBle != null)
            {
                CH9140.CH9140CloseDevice((nint)handleBle);
                bleReadStream.Dispose();
                handleBle = null;
            }
        }

        private async void click_recurrentBtn(object sender, RoutedEventArgs e)
        {
            // Create a file picker
            var openPicker = new Windows.Storage.Pickers.FileOpenPicker();

            // Retrieve the window handle (HWND) of the current WinUI 3 window.
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            // Initialize the file picker with the window handle (HWND).
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

            // Set options for your file picker
            openPicker.FileTypeFilter.Add(".csv");

            // Open the picker for the user to pick a file
            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                // Prevent updates to the remote version of the file until we finish making changes and call CompleteUpdatesAsync.
                CachedFileManager.DeferUpdates(file);
                using var stream = await file.OpenReadAsync();
                using var fr = new StreamReader(stream.AsStreamForRead());
                string[] colString;
                ushort[,] heatmapValue = new ushort[row, col];
                for (int i = 0; i < row; i++)
                {
                    colString = fr.ReadLine().Split(',');
                    for (int j = 0; j < col; j++)
                        heatmapValue[i, j] = Convert.ToUInt16(colString[j]);
                }
                heatMapValue2UI(heatmapValue);
            }
        }

        private void click_heatMapItem(object sender, ItemClickEventArgs e)
        {
            var pixel = (e.ClickedItem as HeatMap_pixel);
            if (pixel.chartLine == null)
            {
                pixel.chartLine = new(pixel);
                //pixel.chartLine.yAxes[0].MaxLimit = Convert.ToInt32(legendRange.Text);
                lineCharts.Add(pixel.chartLine);
                //pixel.chartLine.tokenLegend = legendRange.RegisterPropertyChangedCallback(TextBlock.TextProperty, (s, dp) => {
                //    if (dp == TextBlock.TextProperty)
                //        pixel.chartLine.yAxes[0].MaxLimit = Convert.ToInt32((s as TextBlock).Text);
                //});
            }
        }

        private async void click_CBFSaveIcon(object sender, RoutedEventArgs e)
        {
            var lineChart = (sender as AppBarButton).CommandParameter as CartesianChart;
            FileSavePicker savePicker = new();
            // Retrieve the window handle (HWND) of the current WinUI 3 window.
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            // Initialize the file picker with the window handle (HWND).
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);
            // Dropdown of file types the user can save the file as
            savePicker.FileTypeChoices.Add("line chart", [".png", ".txt"]);
            // Open the picker for the user to pick a file
            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                // Prevent updates to the remote version of the file until we finish making changes and call CompleteUpdatesAsync.
                CachedFileManager.DeferUpdates(file);
                using var stream = await file.OpenStreamForWriteAsync();
                switch (file.FileType)
                {
                case ".png":
                    var skChart = new SKCartesianChart(lineChart);
                    skChart.SaveImage(stream);
                    break;
                case ".txt":
                    using (var sw = new StreamWriter(stream))
                    {
                        var values = (lineChart.Series.First().Values as List<DateTimePoint>);
                        foreach (var item in values)
                            sw.WriteLine(item.Value);
                    }
                    break;
                }
            } 
            lineChart.ContextFlyout.Hide();
        }

        private void click_CBFDeleteIcon(object sender, RoutedEventArgs e)
        {
            var chartLine = (sender as AppBarButton).CommandParameter as ViewModel_lineChart;
            lineCharts.Remove(chartLine);
            (chartLine.series[0].Values as List<DateTimePoint>).Clear();
            legendRange.UnregisterPropertyChangedCallback(TextBlock.TextProperty, chartLine.tokenLegend);
            chartLine.parent.chartLine = null;
        }
    }
}
