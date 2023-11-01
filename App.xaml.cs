using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
            DeviceWatcher deviceWatcher = DeviceInformation.CreateWatcher(SerialDevice.GetDeviceSelector());
            deviceWatcher.Added += (dw, info) => {
                m_window.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () => {
                    m_window.comInfos.Add(info);
                    if(m_window.combobox_com.SelectedItem == null)
                        m_window.combobox_com.SelectedItem = info;
                    m_window.info_error.IsOpen = false;
                });
            };
            deviceWatcher.Removed += (dw, infoUpdate) => {
                m_window.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () => {
                    foreach (DeviceInformation comInfo in m_window.comInfos)
                        if (comInfo.Id == infoUpdate.Id)
                        {
                            m_window.comInfos.Remove(comInfo);
                            if (infoUpdate.Id == m_window.comID && m_window.viewModel_Switch.isStartIcon)
                                m_window.viewModel_Switch.isStartIcon = false;
                            if (m_window.comInfos.Count == 0)
                            {
                                m_window.info_error.IsOpen = true;
                                m_window.info_error.Message = "未找到串口";
                            }
                            break;
                        }
                        
                });
            };
            m_window = new MainWindow();
            deviceWatcher.Start();
            m_window.Activate();

        }

        private MainWindow m_window;
    }
}
