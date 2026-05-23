using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Buttplug;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Dildosu
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        TcpClient newClient;
        TcpListener listener;
        ClientWebSocket socket;

        ButtplugClient buttplugClient;
        ButtplugConnector buttplugConnector;

        int currentCombo = 0;
        int comboMultiplier = 1;
        int currentStatus = 0; 

        public static async Task<ClientWebSocket> GetConnectedWebSocket(
            Uri server,
            int timeOutMilliseconds)
        {
            var cws = new ClientWebSocket();
            var cts = new CancellationTokenSource(timeOutMilliseconds);

            //Debug.WriteLine("GetConnectedWebSocket: ConnectAsync starting.");
            Task taskConnect = cws.ConnectAsync(server, cts.Token);
            await taskConnect;
            //Debug.WriteLine("GetConnectedWebSocket: ConnectAsync done.");
            //Debug.WriteLine("GetConnectedWebSocket: cws state:" + cws.State);

            return cws;
        }

        public static Task SendString(ClientWebSocket ws, String data, CancellationToken cancellation)
        {
            var encoded = Encoding.UTF8.GetBytes(data);
            var buffer = new ArraySegment<Byte>(encoded, 0, encoded.Length);
            return ws.SendAsync(buffer, WebSocketMessageType.Text, true, cancellation);
        }

        public async Task HandleOsuMessages()
        {
            try
            {
                socket = await GetConnectedWebSocket(new Uri("ws://localhost:20727/tokens"), 1000);
                var tokenList = new List<string>();
                tokenList.Add("mapArtistTitle");
                tokenList.Add("mAR");
                tokenList.Add("c300");
                tokenList.Add("c100");
                tokenList.Add("c50");
                tokenList.Add("miss");
                tokenList.Add("combo");
                tokenList.Add("sliderBreaks");
                tokenList.Add("playerHp");
                tokenList.Add("status");


                string tokenListString = JsonConvert.SerializeObject(tokenList);

                await SendString(socket, tokenListString, CancellationToken.None);

                using (var ms = new MemoryStream())
                {
                    while (socket.State == WebSocketState.Open)
                    {
                        WebSocketReceiveResult result;
                        do
                        {
                            var messageBuffer = WebSocket.CreateClientBuffer(2048, 16);
                            result = await socket.ReceiveAsync(messageBuffer, CancellationToken.None);
                            ms.Write(messageBuffer.Array, messageBuffer.Offset, result.Count);
                        }
                        while (!result.EndOfMessage);

                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            var msgString = Encoding.UTF8.GetString(ms.ToArray());
                            Debug.Print(msgString);
                            var message = JsonConvert.DeserializeObject<Dictionary<String, String>>(msgString);

                            this.Dispatcher.Invoke(() =>
                            {
                                if (message == null)
                                    return;

                                if (message.ContainsKey("status"))
                                {
                                    if (message["status"] != "2")
                                    {
                                        Debug.WriteLine("No longer playing, clearing queue");
                                        while (queue.TryTake(out _)) { };
                                        buttplugConnector.device?.SendVibrateCmd(0);
                                    }

                                    StatusLabel.Content = message["status"];
                                    currentStatus = Convert.ToInt32( message["status"]);
                                }

                                if (message.ContainsKey("mapArtistTitle"))
                                {
                                    SongNameLabel.Content = message["mapArtistTitle"];
                                }

                                if (message.ContainsKey("playerHp"))
                                {
                                double currentHealth = Convert.ToDouble(message["playerHp"]);
                                currentHealthBar.Value = currentHealth;

                                    if (healthModeCb.IsChecked == true && message["status"] != "2")
                                        setVibration(Math.Max(0, (150 - currentHealth) / 7.5));
                                }

                                if (message.ContainsKey("combo"))
                                {
                                    int prevCombo = (int)sbCountLabel.Content;
                                    int newCombo = Int32.Parse(message["combo"]);

                                    if (newCombo < prevCombo && comboModeCb.IsChecked == true)
                                        comboMultiplier = 1+ prevCombo / 50;
                                    else comboMultiplier = 1;

                                    currentCombo = newCombo;
                                    comboLabel.Content = message["combo"];

                                    if(pleasureModeCb.IsChecked == true)
                                    {
                                        setVibration(Math.Min(20, currentCombo/10));
                                    }
                                }

                                if (message.ContainsKey("c300"))
                                {
                                    int prevCount = (int)hitCountLabel.Content;
                                    int newCount = Int32.Parse(message["c300"]);

                                    if (newCount > prevCount)
                                        enqueueHitVibration(c300slider.Value, c300time.Value);

                                    hitCountLabel.Content = newCount;
                                }
                                if (message.ContainsKey("c100")) {
                                    int prevCount = (int)hundredCountLabel.Content;
                                    int newCount = Int32.Parse(message["c100"]);

                                    if (newCount > prevCount)
                                        enqueueHitVibration(c100slider.Value, c100time.Value);

                                    hundredCountLabel.Content = newCount;
                                }
                                if (message.ContainsKey("c50"))
                                {
                                    int prevCount = (int)fiftyCountLabel.Content;
                                    int newCount = Int32.Parse(message["c50"]);

                                    if (newCount > prevCount)
                                        enqueueHitVibration(c50slider.Value, c50time.Value);

                                    fiftyCountLabel.Content = newCount;
                                }

                                if (message.ContainsKey("miss"))
                                {
                                    int prevCount = Convert.ToInt32(missCountLabel.Content);
                                    int newCount = Int32.Parse(message["miss"]);

                                    if (newCount > prevCount)
                                    {
                                        var missStrength = Math.Min(20, missSlider.Value * comboMultiplier);
                                        enqueueHitVibration(missStrength, missTime.Value * (comboMultiplier * comboMultiplier));
                                    }

                                    missCountLabel.Content = message["miss"];
                                }
                                if (message.ContainsKey("sliderBreak"))
                                {
                                    int prevCount = (int)sbCountLabel.Content;
                                    int newCount = Int32.Parse(message["sliderBreaks"]);

                                    if (newCount > prevCount)
                                    {
                                        var missStrength = Math.Min(20, sbSlider.Value * comboMultiplier);
                                        enqueueHitVibration(missStrength, sbTime.Value * (comboMultiplier * comboMultiplier));
                                    }


                                    sbCountLabel.Content = message["sliderBreaks"];
                                }

                            });
                        }
                        ms.Seek(0, SeekOrigin.Begin);
                        ms.SetLength(0);
                        ms.Position = 0;
                    }
                }
            }
            catch (InvalidOperationException)
            {
                Debug.Print("[WS] Tried to receive message while already reading one.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in HandleMessages2 - {0}", ex.Message);
            }
        }

        private class VibrationAction{
            public double intensity;
            public double seconds;

            public VibrationAction(double intensity, double seconds)
            {
                this.intensity = intensity;
                this.seconds = seconds;
            }

            public override string ToString()
            {
                return $"{intensity * 5}% power for {seconds} seconds";
            }
        }

        BlockingCollection<VibrationAction> queue = new BlockingCollection<VibrationAction>();
        List<VibrationAction> vibrationList = new List<VibrationAction>();


        private void enqueueHitVibration(double intensity, double seconds)
        {
            if (intensity == 0)
                return;
            if (seconds == 0)
                return;
            if (pleasureModeCb.IsChecked == true)
                return;

            if (healthModeCb.IsChecked == true)
                return;

            var vibration = new VibrationAction(intensity, seconds);            
            vibrationList.Add(vibration);
            vibrationListView.Items.Refresh();  

            queue.Add(vibration);
        }

        private async void setVibration(double strength)
        {
            this.Dispatcher.Invoke(() => {
                vibrationList.Clear();
                vibrationListView.Items.Refresh();
                currentVibrationLabel.Content = $"Vibrating with strength {strength}";
                currentVibrationStrengthPb.Value = strength * 5;
                currentVibrationTimePb.Value = 100; 
            });

            await buttplugConnector.device.SendVibrateCmd(strength/20);
        }


        private async void HandleVibrationQueue()
        {
            double currentVibration = 0;

            //Clear out weird items sitting in the list on boot
            vibrationList.Clear();
            this.Dispatcher.Invoke(() => {
                vibrationListView.Items.Refresh();
            });

            foreach (var vibrationAction in queue.GetConsumingEnumerable())
            {
                if (buttplugConnector == null || buttplugConnector.device == null)
                    continue;

                if (vibrationAction == null)
                {
                    await buttplugConnector.device.SendVibrateCmd(0);
                    continue;
                }

                //if (vibrationAction.intensity < currentVibration && vibrationAction.seconds > 0)
                //    continue;

                vibrationList.Remove(vibrationAction);
                this.Dispatcher.Invoke(() => {
                    vibrationListView.Items.Refresh();
                    currentVibrationLabel.Content = vibrationAction.ToString();
                    currentVibrationStrengthPb.Value = vibrationAction.intensity * 5;
                });

                Debug.WriteLine($"Vibrating with power {vibrationAction.intensity} for {vibrationAction.seconds} seconds");
                await buttplugConnector.device.SendVibrateCmd(vibrationAction.intensity/20);

                int fps = 30; 
                if (vibrationAction.seconds > 0)
                {
                    for (int i = 0; i < vibrationAction.seconds * fps; i++)
                    {
                        Thread.Sleep(1000/fps);
                        this.Dispatcher.Invoke(() =>
                        {
                            currentVibrationTimePb.Value = 100* (i / (vibrationAction.seconds * fps));
                        });
                    }
                }
                this.Dispatcher.Invoke(() =>
                {
                    currentVibrationLabel.Content = "";
                    currentVibrationTimePb.Value = 0;
                    currentVibrationStrengthPb.Value = 0;
                });


                if (queue.Count == 0)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        vibrationList.Clear();
                        vibrationListView.Items.Refresh();
                    });
                    await buttplugConnector.device.SendVibrateCmd(0);
                }

            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            vibrationListView.ItemsSource = null;
            vibrationListView.ItemsSource = vibrationList;

            Task wsTask = new Task(() => { HandleOsuMessages(); });
            wsTask.Start();


            Task vqTask = new Task(() => { HandleVibrationQueue(); });
            vqTask.Start();

            buttplugConnector = new ButtplugConnector();
            buttplugConnector.PropertyChanged += buttplugConnector_PropertyChanged;

            Task buttplugTask = new Task(() => {  buttplugConnector.ConnectWebsocket(); });
            buttplugTask.Start();

            hitCountLabel.Content = 0;
            hundredCountLabel.Content = 0;
            fiftyCountLabel.Content = 0;
            missCountLabel.Content = 0; 
            sbCountLabel.Content = 0;   
            comboLabel.Content = 0; 
        }

        void buttplugConnector_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "ToyStatus":
                    this.Dispatcher.Invoke(() => {
                        toyConnectionLabel.Content = buttplugConnector.ToyStatus;
                    });
                    break;
            }
        }

        async void MainWindow_Closing(object sender, CancelEventArgs e)
        { 
            if(socket != null)
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);

            if (newClient != null)
                newClient.Close();

            if (listener != null)
                listener.Stop();

        }

    }
}
