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
        public DispatcherQueueController thread_serialCollect;

        public ObservableCollection<DeviceInformation> comInfos = new();
        internal ViewModel_switch viewModel_Switch = new();
        internal ObservableCollection<HeatMap_pixel> heatmap = new();

        public MainWindow()
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);
            for (int i = 0; i < row; i++)
                for (int j = 0; j < col; j++)
                    heatmap.Add(new HeatMap_pixel(i, j, 0));
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
                        readerCom = new(com.InputStream);
                        readerCom.ByteOrder = ByteOrder.BigEndian;
                        info_error.IsOpen = false;
                        com.BaudRate = Convert.ToUInt32(combobox_baud.SelectedValue);
                        com.DataBits = Convert.ToUInt16(combobox_dataBits.SelectedValue);
                        com.StopBits = (SerialStopBitCount)combobox_stopBits.SelectedIndex;
                        com.Parity = (SerialParity)combobox_parity.SelectedIndex;
                        com.ReadTimeout = TimeSpan.FromMilliseconds(40);

                        info_error.IsOpen = false;
                        viewModel_Switch.isStartIcon = false;

                        thread_serialCollect = DispatcherQueueController.CreateOnDedicatedThread();
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
                                    thread_serialCollect.ShutdownQueueAsync();
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
                                    for (int i = 0; i < row; i++)
                                        for (int j = 0; j < col; j++)
                                            heatmap[i * col + j].adcValue = heatmapValue[i, j];
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
    }
}
