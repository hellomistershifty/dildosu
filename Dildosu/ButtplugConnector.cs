using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Buttplug;

namespace Dildosu
{
    public class ButtplugConnector : INotifyPropertyChanged
    {
        public ButtplugWebsocketConnectorOptions connector;
        public ButtplugClient client;
        public ButtplugClientDevice device;
        private string _toyStatus = "Not connected";

        protected void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, e);
        }

        protected void OnPropertyChanged(
        [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }

        public string ToyStatus
        {
            get { return _toyStatus; }
            set
            {
                if (value != _toyStatus)
                {
                    _toyStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Connect()
        {
            ConnectWebsocket().Wait();
        }

        public async Task ConnectWebsocket()
        {
            // Creating a Websocket Connector is as easy as using the right
            // options object.
            connector = new ButtplugWebsocketConnectorOptions(
                new Uri("ws://localhost:6969/buttplug"));
            client = new ButtplugClient("Dildosu");
            client.ServerDisconnect += ServerDisconnect;

            while (!client.Connected)
            {
                try
                {
                    ToyStatus = $"Connecting to Intiface...";
                    await client.ConnectAsync(connector);
                }
                catch (Exception ex)
                {
                    ToyStatus = ($"Intiface connection failed.");
                    Debug.WriteLine(ex);
                }
                Thread.Sleep(1000);
            }
                       
            if (client.Connected)
            {
                ToyStatus = ($"Intiface Connected");

                Debug.WriteLine("Intiface Connected");

                await client.StartScanningAsync();

                if (client.Devices.Length > 0) {
                    device = client.Devices[0];
                    ToyStatus = ($"Connected to {device.Name}");
                    Debug.WriteLine("Device connected: " + device.Name);
                }
                else
                {
                    ToyStatus = ($"Failed to find a device. Please retry");
                    Debug.WriteLine("Failed to find a device. Please retry");
                }
                
            }
        }

        private void ServerDisconnect(object sender, EventArgs e)
        {
            Debug.WriteLine("Intiface Disconnected");
        }
    }
}