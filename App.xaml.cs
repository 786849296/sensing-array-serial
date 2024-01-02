using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.System.Threading;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace uart
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            DeviceWatcher deviceWatcherCOM = DeviceInformation.CreateWatcher(SerialDevice.GetDeviceSelector());
            deviceWatcherCOM.Added += (dw, info) => {
                m_window.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () => {
                    m_window.comInfos.Add(info);
                    m_window.combobox_com.SelectedItem ??= info;
                    m_window.info_comOpen = false;
                    m_window.info_com.IsOpen = false;
                });
            };
            deviceWatcherCOM.Removed += (dw, infoUpdate) => {
                m_window.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () => {
                    foreach (DeviceInformation comInfo in m_window.comInfos)
                        if (comInfo.Id == infoUpdate.Id)
                        {
                            if (m_window.pivot.SelectedIndex == 0)
                            {
                                if (infoUpdate.Id == (m_window.combobox_com.SelectedItem as DeviceInformation).Id && m_window.viewModel_Switch.isStartIcon)
                                    m_window.viewModel_Switch.isStartIcon = true;
                                if (m_window.comInfos.Count == 0)
                                {
                                    m_window.info_comOpen = true;
                                    m_window.info_com.IsOpen = true;
                                    m_window.info_com.Message = "未找到串口";
                                }
                                break;
                            }
                            m_window.comInfos.Remove(comInfo);
                        }
                        
                });
            };

            // Query for extra properties you want returned
            string[] requestedProperties = ["System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected"];
            DeviceWatcher deviceWatcherBLE = DeviceInformation.CreateWatcher(
                BluetoothLEDevice.GetDeviceSelectorFromPairingState(false),
                requestedProperties,
                DeviceInformationKind.AssociationEndpoint);
            deviceWatcherBLE.Added += (dw, info) =>
            {
                if (info.Name.StartsWith("CH9143"))
                {
                    m_window.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
                    {
                        m_window.bleInfos.Add(info);
                        m_window.combobox_ble.SelectedItem ??= info;
                        m_window.info_bleOpen = false;
                        m_window.info_ble.IsOpen = false;
                    });
                    //m_window.handleBle = CH9140.CH9140UartOpenDevice(info.Id, null, null, (p, buf, len) =>
                    //{
                    //    byte[] data = new byte[len];
                    //    unsafe {
                    //        fixed (byte* source = buf)
                    //        fixed (byte* destin = data)
                    //            Buffer.MemoryCopy(source, destin, (long)len, (long)len);
                    //    }
                    //    m_window.bleReadStream.Write(data, 0, (int)len);
                    //});
                }
            };
            deviceWatcherBLE.Removed += (dw, infoUpdate) => {
                m_window.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () => {
                    for (int i = 0; i < m_window.bleInfos.Count; i++)
                        if (m_window.bleInfos[i].Id == infoUpdate.Id)
                        {
                            if (m_window.pivot.SelectedIndex == 1)
                            {
                                if (infoUpdate.Id == (m_window.combobox_ble.SelectedItem as DeviceInformation).Id && m_window.viewModel_Switch.isStartIcon)
                                    m_window.viewModel_Switch.isStartIcon = true;
                                if (m_window.bleInfos.Count == 0)
                                {
                                    m_window.info_bleOpen = true;
                                    m_window.info_ble.IsOpen = true;
                                    m_window.info_ble.Message = "未找到蓝牙";
                                }
                                break;
                            }
                            m_window.bleInfos.Remove(m_window.bleInfos[i]);
                        }
                });
            };
            m_window = new MainWindow();
            deviceWatcherCOM.Start();
            deviceWatcherBLE.Start();
            m_window.Activate();
            ThreadPool.RunAsync((item) => CH9140.CH9140Init());
        }

        private MainWindow m_window;
    }
}
