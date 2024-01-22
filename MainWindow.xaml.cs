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
using Microsoft.UI.Dispatching;
using System.Diagnostics;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI;
using Windows.UI;
using Windows.System.Threading;
using Microsoft.UI.Xaml.Media.Imaging;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace uart
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public const ushort row = 10;
        public const ushort col = 16;
        public Queue<ushort[,]> frames = new();

        private StorageFolder folder;
        private DataReader reader;
        private SerialDevice com;
        private nint? handleBle = null;
        private InMemoryRandomAccessStream bleReadStream;
        //private readonly GloveModel model = new();
        private readonly GloveNet model = new();
        //private readonly DispatcherQueueController thread_collect = DispatcherQueueController.CreateOnDedicatedThread();

        private readonly Color[] legendLinearGradientColors =
        [
            Colors.DarkBlue,
            Colors.Blue,
            Colors.Cyan,
            Colors.Yellow,
            Colors.Red,
            Colors.DarkRed,
        ];
        public ObservableCollection<DeviceInformation> comInfos = [];
        public ObservableCollection<DeviceInformation> bleInfos = [];
        internal ViewModel_switch viewModel_Switch = new();
        internal ObservableCollection<HeatMap_pixel> palm = [];
        internal ObservableCollection<HeatMap_pixel> f1 = [];
        internal ObservableCollection<HeatMap_pixel> f2 = [];
        internal ObservableCollection<HeatMap_pixel> f3 = [];
        internal ObservableCollection<HeatMap_pixel> f4 = [];
        internal ObservableCollection<HeatMap_pixel> f5 = [];
        internal ObservableCollection<HeatMap_pixel> ff = [];
        internal ObservableCollection<HeatMap_pixel> fb = [];
        internal bool info_comOpen = true;
        internal bool info_bleOpen = true;
        private int lastPivotIndex;

        public MainWindow()
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);
            for (int i = 0; i < 10; i++)
                for (int j = 0; j < 10; j++)
                    palm.Add(new HeatMap_pixel(i, j + 6, 0));
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    f1.Add(new HeatMap_pixel(i, j, 0));
                    f2.Add(new HeatMap_pixel(i + 3, j, 0));
                    f3.Add(new HeatMap_pixel(i + 6, j, 0));
                    f4.Add(new HeatMap_pixel(i, j + 3, 0));
                    f5.Add(new HeatMap_pixel(i + 3, j + 3, 0));
                }
            ff.Add(new HeatMap_pixel(6, 3, 0));
            ff.Add(new HeatMap_pixel(6, 4, 0));
            ff.Add(new HeatMap_pixel(6, 5, 0));
            ff.Add(new HeatMap_pixel(7, 3, 0));
            ff.Add(new HeatMap_pixel(7, 4, 0));
            fb.Add(new HeatMap_pixel(7, 5, 0));
            fb.Add(new HeatMap_pixel(8, 3, 0));
            fb.Add(new HeatMap_pixel(8, 4, 0));
            fb.Add(new HeatMap_pixel(8, 5, 0));
            fb.Add(new HeatMap_pixel(9, 0, 0));

            lastPivotIndex = pivot.SelectedIndex;
        }

        private void heatMapValue2UI(ushort[,] heatmapValue)
        {
            for (int i = 0; i < row; i++)
                for (int j = 0; j < col; j++)
                    if (i < 3)
                    {
                        if (j < 3)
                            f1[8 - (i * 3 + j)].adcValue = heatmapValue[i, j];
                        else if (j < 6)
                            f4[8 - (i * 3 + j - 3)].adcValue = heatmapValue[i, j];
                        else
                            palm[i * 10 + j - 6].adcValue = heatmapValue[i, j];
                    }
                    else if (i < 6)
                    {
                        if (j < 3)
                            f2[8 - ((i - 3) * 3 + j)].adcValue = heatmapValue[i, j];
                        else if (j < 6)
                            f5[8 - ((i - 3) * 3 + j - 3)].adcValue = heatmapValue[i, j];
                        else
                            palm[i * 10 + j - 6].adcValue = heatmapValue[i, j];
                    }
                    else if (i < 9)
                    {
                        if (j < 3)
                            f3[8 - ((i - 6) * 3 + j)].adcValue = heatmapValue[i, j];
                        else if (j < 6)
                        {
                            if (i < 8)
                                if (i == 7 && j == 5)
                                    fb[0].adcValue = heatmapValue[i, j];
                                else
                                    ff[(i - 6) * 3 + j - 3].adcValue = heatmapValue[i, j];
                            else
                                fb[j - 2].adcValue = heatmapValue[i, j];
                        }
                        else
                            palm[i * 10 + j - 6].adcValue = heatmapValue[i, j];
                    }
                    else
                    {
                        if (j == 0)
                            fb[4].adcValue = heatmapValue[i, j];
                        else if (j >= 6)
                            palm[i * 10 + j - 6].adcValue = heatmapValue[i, j];
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
                                    com.ReadTimeout = TimeSpan.FromMilliseconds(165);
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
                    await reader.LoadAsync(row * col * 2 + 2);
                    bool flag_get = false;
                    while (reader.UnconsumedBufferLength > 0)
                        if (reader.ReadByte() == 0xff)
                            if (reader.ReadByte() == 0xff)
                            {
                                flag_get = true;
                                break;
                            }
                    if (!flag_get)
                        throw new Exception("未找到帧头");
                    while (true)
                    {
                        //放线程里面保证com和reader此时未操作
                        if (viewModel_Switch.isStartIcon)
                        {
                            //thread_collect.ShutdownQueueAsync();
                            if (mode == 0)
                                com.Dispose();
                            reader.Dispose();
                            frames.Clear();
                            return;
                        }
                        await reader.LoadAsync(row * col * 2 + 2);
                        if (reader.UnconsumedBufferLength < row * col * 2 + 2)
                        {
                            //保证读取的数据量
                            if (mode == 1)
                                System.Threading.Thread.Sleep(85);
                            continue;
                        }
                        ushort[,] heatmapValue = new ushort[row, col];
                        for (int i = 0; i < row; i++)
                            for (int j = 0; j < col; j++)
                                heatmapValue[i, j] = reader.ReadUInt16();
                        if (reader.ReadUInt16() != 0xffff)
                            throw new Exception("未找到下一帧帧头");
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            if (ts_modelPredict.IsOn)
                            {
                                //识别时帧采样的手背及指腹点的阈值（进入识别或退出识别）
                                const ushort threshold = 150;
                                ushort[] fb = [heatmapValue[6, 3], heatmapValue[6, 4], heatmapValue[6, 5], heatmapValue[7, 3], heatmapValue[7, 4], heatmapValue[7, 5], heatmapValue[8, 3], heatmapValue[8, 4], heatmapValue[8, 5], heatmapValue[9, 0]];
                                if (fb.Max() > threshold)
                                    frames.Enqueue(heatmapValue);
                                else if (frames.Count > 0)
                                {
                                    if (frames.Count >= KeyFrames.framesNum)
                                    {
                                        Stopwatch stopwatch = new();
                                        stopwatch.Start();
                                        (string label, float val) = model.predict(frames).getResult();
                                        stopwatch.Stop();
                                        text_predict.Text = label;
                                        text_cost.Text = $"frame num: {frames.Count} / time: {stopwatch.Elapsed.TotalMilliseconds:f1} ms";
                                        image_predict.Source = new BitmapImage(new Uri($"ms-appx:///Assets//{label}.png"));
                                        text_score.Text = $"{val:f2}%";
                                        pr_score.Value = val;
                                    }
                                    frames.Clear();
                                }
                            }
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
            foreach (var item in palm)
                item.range = range;
            foreach (var item in f1)
                item.range = range;
            foreach (var item in f2)
                item.range = range;
            foreach (var item in f3)
                item.range = range;
            foreach (var item in f4)
                item.range = range;
            foreach (var item in f5)
                item.range = range;
            foreach (var item in ff)
                item.range = range;
            foreach (var item in fb)
                item.range = range;
            if (legendRange != null)
                legendRange.Text = range.ToString();
        }

        private void tapped_pixel(object sender, TappedRoutedEventArgs e)
        {
            ChartWindow chartLine = new();
            chartLine.yAxes[0].MaxLimit = Convert.ToInt32(legendRange.Text);
            chartLine.AppWindow.Resize(new Windows.Graphics.SizeInt32(720, 720));
            chartLine.Activate();
            var tokenColor = ((sender as Rectangle).Fill as SolidColorBrush).RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, (s, dp) => {
                if (dp == SolidColorBrush.ColorProperty)
                {
                    int adcValue = 0;
                    Color color = (s as SolidColorBrush).Color;
                    if (color != Colors.White)
                    {
                        for (int i = 0; i < legendLinearGradientColors.Length; i++)
                            if (color == legendLinearGradientColors[i])
                                // 如果找到，直接返回对应的 ADC 值
                                adcValue = i * Convert.ToInt32(legendRange.Text) / (legendLinearGradientColors.Length - 1);
                        if (adcValue == 0)
                        {
                            int region = -1;
                            float offset = 0;
                            // 遍历颜色数组，查找给定的颜色所在的区间
                            for (int i = 0; i < legendLinearGradientColors.Length - 1; i++)
                            {
                                // 获取区间左端和右端的颜色值
                                Color c1 = legendLinearGradientColors[i];
                                Color c2 = legendLinearGradientColors[i + 1];
                                // 判断给定的颜色是否在区间内，即 R、G、B 值都在区间范围内
                                if (color.R >= Math.Min(c1.R, c2.R) && color.R <= Math.Max(c1.R, c2.R) &&
                                    color.G >= Math.Min(c1.G, c2.G) && color.G <= Math.Max(c1.G, c2.G) &&
                                    color.B >= Math.Min(c1.B, c2.B) && color.B <= Math.Max(c1.B, c2.B))
                                {
                                    // 如果在区间内，记录区间索引
                                    region = i;
                                    // 解线性方程组，计算 offset 值，这里假设颜色值不为 0
                                    // 如果颜色值为 0，可以用另一种方法计算 offset，例如使用 G 或 B 值
                                    if (c2.R - c1.R != 0)
                                        offset = (float)(color.R - c1.R) / (c2.R - c1.R);
                                    else if (c2.G - c1.G != 0)
                                        offset = (float)(color.G - c1.G) / (c2.G - c1.G);
                                    else if (c2.B - c1.B != 0)
                                        offset = (float)(color.B - c1.B) / (c2.B - c1.B);
                                    // 跳出循环
                                    break;
                                }
                            }
                            // 如果找到了区间，返回 ADC 值，否则返回 0
                            if (region != -1)
                                adcValue = (int)((region + offset) * Convert.ToInt32(legendRange.Text) / (legendLinearGradientColors.Length - 1));
                        }
                    }
                    var lineSerieData = chartLine.series[0].Values as ObservableCollection<int>;
                    lineSerieData.Add(adcValue);
                    if (lineSerieData.Count > chartLine.xAxes[0].MaxLimit)
                    {
                        chartLine.xAxes[0].MaxLimit++;
                        chartLine.xAxes[0].MinLimit++;
                    }
                }
            });
            var tokenLegend = legendRange.RegisterPropertyChangedCallback(TextBlock.TextProperty, (s, dp) => {
                if (dp == TextBlock.TextProperty)
                    chartLine.yAxes[0].MaxLimit = Convert.ToInt32((s as TextBlock).Text);
            });
            chartLine.Closed += (s, e) => {
                ((sender as Rectangle).Fill as SolidColorBrush).UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, tokenColor);
                legendRange.UnregisterPropertyChangedCallback(TextBlock.TextProperty, tokenLegend);
            };
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

        private void toggle_modelPredictSw(object sender, RoutedEventArgs e)
        {
            ToggleSwitch modelPredictSw = sender as ToggleSwitch;
            if (modelPredictSw.IsOn)
            {
                // model.initialize();
                model.loadModel(model.modelLocation);
                popup_predict.IsOpen = true;
            }
            else
            {
                model.Dispose();
                frames.Clear();
                popup_predict.IsOpen = false;
            }
        }
    }
}
