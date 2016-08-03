using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SerialRFIDReader
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private SerialDevice serialPort;
        private DataReader dataReader;
        private ObservableCollection<DeviceInformation> deviceList;
        private CancellationTokenSource readCancellationTokenSource;
         
        public MainPage()
        {
            InitializeComponent();

            serialPort = null;
            dataReader = null;
            deviceList = new ObservableCollection<DeviceInformation>();
            readCancellationTokenSource = null;

            connectButton.IsEnabled = false;
            disconnectButton.IsEnabled = false;

            ListAvailablePorts();
        }

        private async void ListAvailablePorts()
        {
            try {
                string devSel = SerialDevice.GetDeviceSelector();
                var dis = await DeviceInformation.FindAllAsync(devSel);

                statusTextBox.Text = "Select a device and connect.";

                for (int i = 0; i < dis.Count; i++) {
                    deviceList.Add(dis[i]);
                }

                deviceListSource.Source = deviceList;
                serialDeviceListBox.SelectedIndex = -1;
            } catch (Exception ex) {
                statusTextBox.Text = ex.Message;
            }
        }

        private async void ConnectButtonClickAction(object sender, RoutedEventArgs e)
        {
            var selection = serialDeviceListBox.SelectedItems;

            if (selection.Count <= 0) {
                statusTextBox.Text = "Select a device and connect.";
                return;
            }

            DeviceInformation entry = (DeviceInformation)selection[0];

            try {
                serialPort = await SerialDevice.FromIdAsync(entry.Id);

                connectButton.IsEnabled = false;
                disconnectButton.IsEnabled = true;
                serialDeviceListBox.IsEnabled = false;

                // Configure serial settings
                serialPort.ReadTimeout = TimeSpan.FromMilliseconds(1000);
                serialPort.BaudRate = 9600;
                serialPort.DataBits = 8;
                serialPort.StopBits = SerialStopBitCount.One;
                serialPort.Parity = SerialParity.None;
                serialPort.Handshake = SerialHandshake.None;

                statusTextBox.Text = "Serial port configured successfully! Wating for data...";

                readCancellationTokenSource = new CancellationTokenSource();

                Listen();
            } catch (Exception ex) {
                statusTextBox.Text = ex.Message;
                connectButton.IsEnabled = true;
                disconnectButton.IsEnabled = false;
                serialDeviceListBox.IsEnabled = true;
                serialPort = null;
            }
        }

        private void DisconnectButtonClickAction(object sender, RoutedEventArgs e)
        {
            serialDeviceListBox.IsEnabled = true;
            disconnectButton.IsEnabled = false;

            try {
                CancelReadTask();
                CloseDevice();
                statusTextBox.Text = "Device disconnected.";
                ListAvailablePorts();
            } catch (Exception ex) {
                statusTextBox.Text = ex.Message;
            }
        }

        private async void Listen()
        {
            if (serialPort == null) {
                return;
            }

            try {
                dataReader = new DataReader(serialPort.InputStream);

                while (true) {
                    await ReadAsync(readCancellationTokenSource.Token);
                }
            } catch (Exception ex) {
                if (ex.GetType().Name == "TaskCanceledException") {
                    statusTextBox.Text = "Reading cancelled, cleaning up...";
                    CloseDevice();
                } else {
                    statusTextBox.Text = ex.Message;
                }
            } finally {
                if (dataReader != null) {
                    dataReader.DetachStream();
                    dataReader = null;
                }
            }
        }

        private async Task ReadAsync(CancellationToken cancellationToken)
        {
            Task<UInt32> loadAsyncTask;

            uint ReadBufferLength = 1024;

            // Handle task cancellation request
            cancellationToken.ThrowIfCancellationRequested();

            // Set input stream to complete async read when one or more bytes available
            dataReader.InputStreamOptions = InputStreamOptions.Partial;

            // Create a task object and wait for data on the input stream
            loadAsyncTask = dataReader.LoadAsync(ReadBufferLength).AsTask(cancellationToken);

            // Launch the async task and wait
            UInt32 bytesRead = await loadAsyncTask;
            if (bytesRead > 0) {
                outputTextBox.Text += dataReader.ReadString(bytesRead) + "\n";
                statusTextBox.Text = "Bytes read successfully!";
            }
        }

        private void CancelReadTask()
        {
            if (readCancellationTokenSource != null) {
                if (!readCancellationTokenSource.IsCancellationRequested) {
                    readCancellationTokenSource.Cancel();
                }
            }
        }

        private void CloseDevice()
        {
            if (serialPort != null) {
                serialPort.Dispose();
            }
            serialPort = null;

            connectButton.IsEnabled = true;
            outputTextBox.Text = "";
            deviceList.Clear();
        }

        private void DeviceListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selection = serialDeviceListBox.SelectedItems;
            connectButton.IsEnabled = selection.Count != 0;
        }
    }
}
