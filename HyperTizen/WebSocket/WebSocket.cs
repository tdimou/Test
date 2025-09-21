using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using HyperTizen.WebSocket.DataTypes;
using Rssdp;
using Tizen.Applications;
using static HyperTizen.WebSocket.DataTypes.SSDPScanResultEvent;

namespace HyperTizen.WebSocket
{
    public class WSServer
    {
        // --- ΣΤΑΘΕΡΑ ΔΙΕΥΘΥΝΣΗ/ΘΥΡΕΣ ---
        private const string FixedServerIp = "192.168.200.72";
        private const int HttpPort = 8086;
        private const int WsPort   = 8090;
        private static readonly string FixedWsUri = $"ws://{FixedServerIp}:{WsPort}";

        private readonly HttpListener _httpListener;
        private readonly List<string> usnList = new List<string>()
        {
            "urn:hyperion-project.org:device:basic:1",
            "urn:hyperhdr.eu:device:basic:1"
        };

        public WSServer(string uriPrefix)
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add(uriPrefix);
        }

        public async Task StartAsync()
        {
            _httpListener.Start();
            while (true)
            {
                var httpContext = await _httpListener.GetContextAsync();
                if (httpContext.Request.IsWebSocketRequest)
                {
                    var wsContext = await httpContext.AcceptWebSocketAsync(null);
                    _ = HandleWebSocketAsync(wsContext.WebSocket);
                }
                else
                {
                    httpContext.Response.StatusCode = 400;
                    httpContext.Response.Close();
                }
            }
        }

        private async Task HandleWebSocketAsync(System.Net.WebSockets.WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            while (result.MessageType != WebSocketMessageType.Close)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await OnMessageAsync(webSocket, message);
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }

            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        }

        protected async Task OnMessageAsync(System.Net.WebSockets.WebSocket webSocket, string message)
        {
            BasicEvent data = JsonConvert.DeserializeObject<BasicEvent>(message);

            switch (data.Event)
            {
                case Event.ScanSSDP:
                {
                    var devices = await ScanSSDPAsync();
                    string resultEvent = JsonConvert.SerializeObject(new SSDPScanResultEvent(devices));
                    await SendAsync(webSocket, resultEvent);
                    break;
                }

                case Event.ReadConfig:
                {
                    ReadConfigEvent readConfigEvent = JsonConvert.DeserializeObject<ReadConfigEvent>(message);
                    string result = await ReadConfigAsync(readConfigEvent);
                    await SendAsync(webSocket, result);
                    break;
                }

                case Event.SetConfig:
                {
                    SetConfigEvent setConfigEvent = JsonConvert.DeserializeObject<SetConfigEvent>(message);
                    SetConfiguration(setConfigEvent);
                    break;
                }
            }
        }

        private async Task<List<SSDPDevice>> ScanSSDPAsync()
        {
            var devices = new List<SSDPDevice>();
            using (var deviceLocator = new SsdpDeviceLocator())
            {
                var foundDevices = await deviceLocator.SearchAsync();
                foreach (var foundDevice in foundDevices)
                {
                    if (!usnList.Contains(foundDevice.NotificationType)) continue;

                    var fullDevice = await foundDevice.GetDeviceInfo();
                    Uri descLocation = foundDevice.DescriptionLocation;
                    devices.Add(new SSDPDevice(fullDevice.FriendlyName, descLocation.OriginalString.Replace(descLocation.PathAndQuery, "")));
                }
            }
            return devices;
        }

        private async Task<string> ReadConfigAsync(ReadConfigEvent readConfigEvent)
        {
            string result;
            if (!Preference.Contains(readConfigEvent.key))
            {
                result = JsonConvert.SerializeObject(new ReadConfigResultEvent(true, readConfigEvent.key, "Key doesn't exist."));
            }
            else
            {
                string value = Preference.Get<string>(readConfigEvent.key);
                result = JsonConvert.SerializeObject(new ReadConfigResultEvent(false, readConfigEvent.key, value));
            }
            return result;
        }

        private async Task SendAsync(System.Net.WebSockets.WebSocket webSocket, string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        // Κλειδώνουμε το rpcServer στη σταθερή τιμή, ώστε να μην αλλάζει από μηνύματα
        void SetConfiguration(SetConfigEvent setConfigEvent)
        {
            switch (setConfigEvent.key)
            {
                case "rpcServer":
                {
                    App.Configuration.RPCServer = FixedWsUri;
                    App.client.UpdateURI(FixedWsUri); // ακόμη κι αν το UpdateURI επιτρέπεται, το στέλνουμε στη σταθερή
                    break;
                }
                case "enabled":
                {
                    bool value = bool.Parse(setConfigEvent.value);
                    if (!App.Configuration.Enabled && value)
                    {
                        App.Configuration.Enabled = value;
                        Task.Run(() => App.client.Start(value));
                    }
                    else App.Configuration.Enabled = value;
                    break;
                }
            }

            // Μην αποθηκεύεις διαφορετική τιμή για το rpcServer — παραμένει σταθερό
            if (setConfigEvent.key != "rpcServer")
                Preference.Set(setConfigEvent.key, setConfigEvent.value);
            else
                Preference.Set("rpcServer", FixedWsUri);
        }
    }

    public static class WebSocketServer
    {
        // Δένουμε τον listener ΜΟΝΟ στη σταθερή IP
        public static async Task StartServerAsync()
        {
            var wsServer = new WSServer("http://" + W
