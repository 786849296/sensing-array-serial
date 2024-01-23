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
        public ushort[,] heatmapValue = new ushort[row, col];

        public SerialDevice com;
        public string comID;
        public DataReader readerCom;
        public DispatcherQueueController thread_serialCollect = DispatcherQueueController.CreateOnDedicatedThread();
        public StorageFolder folder;

        public ObservableCollection<DeviceInformation> comInfos = [];
        internal ViewModel_switch viewModel_Switch = new();
        internal ObservableCollection<HeatMap_pixel> heatmap = [];

        public MainWindow()
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);
            for (int i = 0; i < row; i++)
                for (int j = 0; j < col; j++)
                    heatmap.Add(new HeatMap_pixel(i, j));
        }

        private void click_splitViewPaneBtn(object sender, RoutedEventArgs e)
        {
            sv.IsPaneOpen = true;
        }

        private async void click_startBtn(object sender, RoutedEventArgs e)
        {
            if (viewModel_Switch.isStartIcon && combobox_com.SelectedItem != null)
            {
                try
                {
                    com = await SerialDevice.FromIdAsync((combobox_com.SelectedItem as DeviceInformation).Id);
                    comID = (combobox_com.SelectedItem as DeviceInformation).Id;
                    if (com != null)
                    {
                        readerCom = new(com.InputStream)
                        {
                            ByteOrder = ByteOrder.BigEndian
                        };
                        info_error.IsOpen = false;
                        com.BaudRate = Convert.ToUInt32(combobox_baud.SelectedValue);
                        com.DataBits = Convert.ToUInt16(combobox_dataBits.SelectedValue);
                        com.StopBits = (SerialStopBitCount)combobox_stopBits.SelectedIndex;
                        com.Parity = (SerialParity)combobox_parity.SelectedIndex;
                        com.ReadTimeout = TimeSpan.FromMilliseconds(400);

                        info_error.IsOpen = false;
                        viewModel_Switch.isStartIcon = false;
                        
                        thread_serialCollect.DispatcherQueue.TryEnqueue(async () =>
                        {
                            await readerCom.LoadAsync(row * col * 2);
                            bool flag_get = false;
                            while (readerCom.UnconsumedBufferLength > 0)
                                if (readerCom.ReadByte() == 0xff)
                                    if (readerCom.ReadByte() == 0xff)
                                    {
                                        flag_get = true;
                                        break;
                                    }
                            if (!flag_get)
                                throw new Exception("未找到帧头");
                            while (true)
                            {
                                if (viewModel_Switch.isStartIcon)
                                {
                                    com.Dispose();
                                    comID = null;
                                    readerCom.Dispose();
                                    readerCom = null;
                                    return;
                                }
                                await readerCom.LoadAsync(row * col * 2 + 2);
                                for (int i = 0; i < row; i++)
                                    for (int j = 0; j < col; j++)
                                        heatmapValue[i, j] = readerCom.ReadUInt16();
                                if (readerCom.ReadUInt16() != 0xffff)
                                    throw new Exception("未找到下一帧帧头");
                                this.DispatcherQueue.TryEnqueue(() => {
                                    if (folder != null)
                                    {
                                        string fileName = DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss.fff") + ".csv";
                                        using (var writer = new StreamWriter(System.IO.Path.Combine(folder.Path, fileName)))
                                            for (int i = 0; i < row; i++)
                                            {
                                                for (int j = 0; j < col; j++)
                                                    writer.Write(heatmapValue[i, j] + ",");
                                                writer.Write('\n');
                                            }
                                    }
                                    for (int i = 0; i < row; i++)
                                        for (int j = 0; j < col; j++)
                                        {
                                            heatmap[i * col + j].adcValue = heatmapValue[i, j];
                                            if (heatmap[i * col + j].chartLine != null)
                                            {
                                                var lineSerieData = heatmap[i * col + j].chartLine.series[0].Values as ObservableCollection<int>;
                                                lineSerieData.Add(heatmap[i * col + j].adcValue);
                                                if (lineSerieData.Count > heatmap[i * col + j].chartLine.xAxes[0].MaxLimit)
                                                {
                                                    heatmap[i * col + j].chartLine.xAxes[0].MaxLimit++;
                                                    heatmap[i * col + j].chartLine.xAxes[0].MinLimit++;
                                                }
                                            }
                                        }
                                });
                            }
                        });
                    }
                } catch (Exception error)
                {
                    info_error.IsOpen = true;
                    info_error.Message = error.ToString();
                }
            }
            else if (!viewModel_Switch.isStartIcon)
                viewModel_Switch.isStartIcon = true;
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
                for (int i = 0; i < row; i++)
                    for (int j = 0; j < col; j++)
                        heatmap[i * col + j].adcValue = heatmapValue[i, j];
            }
        }

        private void click_heatMapItem(object sender, ItemClickEventArgs e)
        {
            var pixel = (e.ClickedItem as HeatMap_pixel);
            if (pixel.chartLine == null)
            {
                pixel.chartLine = new();
                pixel.chartLine.yAxes[0].MaxLimit = Convert.ToInt32(legendRange.Text);
                pixel.chartLine.AppWindow.Resize(new Windows.Graphics.SizeInt32(720, 720));
                pixel.chartLine.Activate();
                var tokenLegend = legendRange.RegisterPropertyChangedCallback(TextBlock.TextProperty, (s, dp) => {
                    if (dp == TextBlock.TextProperty)
                        pixel.chartLine.yAxes[0].MaxLimit = Convert.ToInt32((s as TextBlock).Text);
                });
                pixel.chartLine.Closed += (s, e) => {
                    legendRange.UnregisterPropertyChangedCallback(TextBlock.TextProperty, tokenLegend);
                    pixel.chartLine = null;
                };
            }
        }
    }
}
